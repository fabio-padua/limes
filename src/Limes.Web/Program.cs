using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Limes.Agents;
using Limes.Agents.Knowledge;
using Limes.Agents.Maf;
using Limes.Agents.Pipeline;
using Limes.Core.Domain;
using Limes.Core.Intake;
using Limes.Core.Reporting;
using Limes.Web;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);
// Bound the assessment cache so repeated runs can't grow memory without limit. Each entry
// counts as size 1, so this caps the number of retained deliverables.
builder.Services.AddMemoryCache(o => o.SizeLimit = 256);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Enum values surface as their names (e.g. "QuickWin") to match the display-name DTOs.
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter() },
};

// How long a generated deliverable stays downloadable after the run that produced it.
// Sliding keeps active assessments alive between downloads; absolute caps total retention
// so a client that keeps downloading can't pin an entry indefinitely.
var cacheSlidingTtl = TimeSpan.FromHours(1);
var cacheAbsoluteTtl = TimeSpan.FromHours(4);

// Cap intake size so a single request can't force the server to buffer an unbounded body.
const long maxIntakeBytes = 1 * 1024 * 1024; // 1 MB is generous for an intake JSON.

// The Minerva grounding corpus is embedded and static, so load+parse it once and reuse the
// instance across agents-mode requests rather than re-reading the assembly resource each time.
var minervaKnowledge = new Lazy<MinervaKnowledgeSource?>(LoadMinervaKnowledge);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Serve the bundled sample intake (embedded resource) so the UI's "Load sample" works everywhere.
app.MapGet("/api/sample", async () =>
{
    await using var stream = typeof(Program).Assembly.GetManifestResourceStream("Limes.Web.sample-intake.json");
    if (stream is null)
        return Results.NotFound(new { error = "Sample intake is not bundled." });
    using var reader = new StreamReader(stream, Encoding.UTF8);
    return Results.Text(await reader.ReadToEndAsync(), "application/json", Encoding.UTF8);
});

// Serve the canonical questionnaire (from Limes.Core.QuestionBank) so the guided survey UI can
// render the questions without embedding domain knowledge in the front end.
app.MapGet("/api/questionnaire", () => Results.Json(QuestionnaireDto.Build(), jsonOptions));

// Run the deterministic pipeline over a posted intake ($0 model cost, no Azure) and cache the
// deliverable so the report artifacts can be downloaded by id.
app.MapPost("/api/assess", async (HttpRequest request, IMemoryCache cache, ILogger<Program> logger, CancellationToken ct) =>
{
    // Reject oversized payloads up front when the length is declared.
    if (request.ContentLength is long declared && declared > maxIntakeBytes)
        return Results.Json(
            new { error = "Intake payload is too large." },
            statusCode: StatusCodes.Status413PayloadTooLarge);

    string body;
    try
    {
        body = await ReadBoundedAsync(request.Body, maxIntakeBytes, ct);
    }
    catch (InvalidDataException)
    {
        // Chunked/unknown-length bodies are caught here once they exceed the cap.
        return Results.Json(
            new { error = "Intake payload is too large." },
            statusCode: StatusCodes.Status413PayloadTooLarge);
    }

    if (string.IsNullOrWhiteSpace(body))
        return Results.BadRequest(new { error = "Request body is empty. Provide intake JSON." });

    AssessmentIntake intake;
    try
    {
        intake = IntakeLoader.FromJson(body);
    }
    catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
    {
        return Results.BadRequest(new { error = $"Invalid intake JSON: {ex.Message}" });
    }

    // Mode selection: deterministic (default, $0, no Azure) or agents (Foundry-backed narrative).
    // Parse strictly — an unknown value (e.g. a "?mode=agent" typo) is a 400 rather than a silent
    // downgrade to deterministic, matching the CLI's strict mode parsing.
    var requestedMode = request.Query["mode"].ToString();
    AssessmentMode mode;
    if (string.IsNullOrEmpty(requestedMode) ||
        string.Equals(requestedMode, "deterministic", StringComparison.OrdinalIgnoreCase))
    {
        mode = AssessmentMode.Deterministic;
    }
    else if (string.Equals(requestedMode, "agents", StringComparison.OrdinalIgnoreCase))
    {
        mode = AssessmentMode.Agents;
    }
    else
    {
        return Results.BadRequest(new
        {
            error = $"Unknown mode '{requestedMode}'. Use 'deterministic' or 'agents'.",
        });
    }

    LimesPipeline pipeline;
    if (mode == AssessmentMode.Agents)
    {
        var connection = FoundryConnection.FromEnvironment(out var reason, out var problem);
        if (connection is null)
        {
            // The EndpointInvalid reasons embed the raw endpoint value; don't leak it to the caller.
            // Log it server-side for diagnosis and return a generic detail instead.
            string? detail;
            if (problem == FoundryConnection.FoundryConfigProblem.EndpointInvalid)
            {
                logger.LogWarning("Agents mode misconfigured: {Reason}", reason);
                detail = $"{FoundryConnection.EndpointEnvVar} is set but invalid.";
            }
            else
            {
                detail = reason;
            }

            return Results.Json(
                new
                {
                    error = "Agents mode is not configured on this server.",
                    detail,
                    hint = $"Set {FoundryConnection.EndpointEnvVar} and {FoundryConnection.DeploymentEnvVar}, or run in deterministic mode.",
                },
                statusCode: StatusCodes.Status409Conflict);
        }

        var factory = new FoundryAgentFactory(connection);
        pipeline = LimesPipelineFactory.CreateAgents(factory, minervaKnowledge.Value);
    }
    else
    {
        pipeline = LimesPipelineFactory.CreateDeterministic();
    }

    AssessmentDeliverable deliverable;
    try
    {
        deliverable = await pipeline.RunAsync(intake, mode, ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // The intake parsed but the pipeline failed — that's a server-side fault. Log the detail
        // server-side and return a stable, non-sensitive payload so we don't leak internals.
        logger.LogError(ex, "Assessment pipeline failed.");
        return Results.Json(
            new { error = "The assessment could not be completed due to an internal error." },
            statusCode: StatusCodes.Status500InternalServerError);
    }

    var id = Guid.NewGuid().ToString("N");
    cache.Set(id, deliverable, new MemoryCacheEntryOptions
    {
        Size = 1,
        SlidingExpiration = cacheSlidingTtl,
        AbsoluteExpirationRelativeToNow = cacheAbsoluteTtl,
    });

    return Results.Json(AssessmentResponse.From(id, deliverable), jsonOptions);
});

// Download any of the five report artifacts for a previously-run assessment.
app.MapGet("/api/assessments/{id}/download/{format}", (string id, string format, IMemoryCache cache) =>
{
    if (!cache.TryGetValue(id, out AssessmentDeliverable? deliverable) || deliverable is null)
        return Results.NotFound(new { error = "Assessment not found or expired. Run the assessment again." });

    var slug = Slug(deliverable.Assessment.Partner.Name);
    return format.ToLowerInvariant() switch
    {
        "json" => Results.File(
            Encoding.UTF8.GetBytes(JsonReportWriter.Write(deliverable)),
            "application/json", $"assessment-{slug}.json"),
        "md" => Results.File(
            Encoding.UTF8.GetBytes(MarkdownReportWriter.Write(deliverable)),
            "text/markdown", $"assessment-{slug}.md"),
        "html" => Results.File(
            Encoding.UTF8.GetBytes(HtmlReportWriter.Write(deliverable)),
            "text/html", $"assessment-{slug}.html"),
        "docx" => Results.File(
            DocxReportWriter.Write(deliverable),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"assessment-{slug}.docx"),
        "pptx" => Results.File(
            PptxReportWriter.Write(deliverable),
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            $"assessment-{slug}.pptx"),
        _ => Results.BadRequest(new { error = $"Unknown format '{format}'. Use json, md, html, docx, or pptx." }),
    };
});

app.Run();

static string Slug(string name)
{
    var slug = string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    if (slug.Length > 60)
        slug = slug[..60];
    return string.IsNullOrEmpty(slug) ? "partner" : slug;
}

// Loads the embedded Minerva grounding corpus for agents mode. Returns null (ungrounded) if the
// resource is missing or unreadable, so agents mode still runs rather than failing on grounding.
static MinervaKnowledgeSource? LoadMinervaKnowledge()
{
    const string resourceName = "Limes.Web.ai-coe-knowledge.md";
    try
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return new MinervaKnowledgeSource(resourceName, reader.ReadToEnd());
    }
    catch (IOException)
    {
        return null;
    }
    catch (NotSupportedException)
    {
        return null;
    }
}

// Reads the request body into a string, throwing InvalidDataException once the byte count
// exceeds maxBytes so a missing/forged Content-Length can't drive unbounded buffering.
static async Task<string> ReadBoundedAsync(Stream body, long maxBytes, CancellationToken ct)
{
    var buffer = new byte[8192];
    using var ms = new MemoryStream();
    int read;
    while ((read = await body.ReadAsync(buffer, ct)) > 0)
    {
        if (ms.Length + read > maxBytes)
            throw new InvalidDataException("Payload exceeds the maximum allowed size.");
        ms.Write(buffer, 0, read);
    }
    return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
}

// Exposed so the integration-test WebApplicationFactory can boot this app.
public partial class Program { }

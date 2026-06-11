using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Limes.Agents;
using Limes.Core.Domain;
using Limes.Core.Intake;
using Limes.Core.Reporting;
using Limes.Web;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Enum values surface as their names (e.g. "QuickWin") to match the display-name DTOs.
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter() },
};

// How long a generated deliverable stays downloadable after the run that produced it.
var cacheTtl = TimeSpan.FromHours(1);

// Cap intake size so a single request can't force the server to buffer an unbounded body.
const long maxIntakeBytes = 1 * 1024 * 1024; // 1 MB is generous for an intake JSON.

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

    AssessmentDeliverable deliverable;
    try
    {
        var pipeline = LimesPipelineFactory.CreateDeterministic();
        deliverable = await pipeline.RunAsync(intake, AssessmentMode.Deterministic, ct);
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
    cache.Set(id, deliverable, new MemoryCacheEntryOptions { SlidingExpiration = cacheTtl });

    return Results.Json(AssessmentResponse.From(id, deliverable), jsonOptions);
});

// Download any of the four report artifacts for a previously-run assessment.
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
        "docx" => Results.File(
            DocxReportWriter.Write(deliverable),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"assessment-{slug}.docx"),
        "pptx" => Results.File(
            PptxReportWriter.Write(deliverable),
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            $"assessment-{slug}.pptx"),
        _ => Results.BadRequest(new { error = $"Unknown format '{format}'. Use json, md, docx, or pptx." }),
    };
});

app.Run();

static string Slug(string name)
{
    var slug = string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    return string.IsNullOrEmpty(slug) ? "partner" : slug;
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

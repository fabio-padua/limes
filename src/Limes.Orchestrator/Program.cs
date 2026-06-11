using Azure.Identity;
using Limes.Agents;
using Limes.Agents.Knowledge;
using Limes.Agents.Maf;
using Limes.Agents.Pipeline;
using Limes.Core.Domain;
using Limes.Core.Intake;
using Limes.Core.Reporting;
using Limes.Orchestrator.Storage;

// Limes orchestrator — Phase 2.
// Usage: Limes.Orchestrator <intake> [output] [--mode deterministic|agents] [--knowledge <path>]
//
// <intake> and [output] may each be a local path OR an Azure Blob URL (https://...). When run as
// a Container Apps Job they can also be supplied via LIMES_INTAKE / LIMES_OUTPUT, and the mode via
// LIMES_MODE — so the job runs with no CLI args. Blob access uses DefaultAzureCredential.
//
//   deterministic (default) — pure rules engine, $0 model cost, no Azure required.
//   agents                  — MAF + Foundry pipeline; requires LIMES_FOUNDRY_ENDPOINT and
//                             LIMES_FOUNDRY_DEPLOYMENT (auth via DefaultAzureCredential).

var parsed = OrchestratorArgs.Parse(args);
if (parsed is null)
    return 1;

// One credential for both Blob access and the Foundry agents (managed identity in Azure,
// az login locally). Token acquisition is lazy, so this is free when neither path is used.
var credential = new DefaultAzureCredential();

AssessmentIntake intake;
try
{
    if (RemoteIo.IsAzureBlobUrl(parsed.IntakePath))
    {
        var json = await RemoteIo.ReadAllTextAsync(parsed.IntakePath, credential);
        intake = IntakeLoader.FromJson(json);
    }
    else
    {
        if (!File.Exists(parsed.IntakePath))
        {
            Console.Error.WriteLine($"Intake file not found: {parsed.IntakePath}");
            return 1;
        }
        intake = await IntakeLoader.FromFileAsync(parsed.IntakePath);
    }
}
catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
{
    Console.Error.WriteLine($"Could not load intake from '{parsed.IntakePath}': {ex.Message}");
    return 1;
}

var outputIsBlob = RemoteIo.IsAzureBlobUrl(parsed.OutputDir);
if (!outputIsBlob)
{
    try
    {
        Directory.CreateDirectory(parsed.OutputDir);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
        or ArgumentException or NotSupportedException or PathTooLongException)
    {
        Console.Error.WriteLine($"Could not create output directory '{parsed.OutputDir}': {ex.Message}");
        return 1;
    }
}

LimesPipeline pipeline;
if (parsed.Mode == AssessmentMode.Agents)
{
    var connection = FoundryConnection.FromEnvironment(out var reason);
    if (connection is null)
    {
        Console.Error.WriteLine($"Agents mode requires Foundry configuration: {reason}");
        Console.Error.WriteLine($"Set {FoundryConnection.EndpointEnvVar} and {FoundryConnection.DeploymentEnvVar}, or run --mode deterministic.");
        return 2;
    }

    // Optional Minerva grounding corpus — only loaded in agents mode, where it's actually used.
    MinervaKnowledgeSource? knowledge = null;
    if (!string.IsNullOrWhiteSpace(parsed.KnowledgePath))
    {
        knowledge = MinervaKnowledgeSource.TryLoadFile(parsed.KnowledgePath);
        if (knowledge is null)
            Console.Error.WriteLine($"Warning: knowledge file could not be loaded, continuing ungrounded: {parsed.KnowledgePath}");
    }

    var factory = new FoundryAgentFactory(connection, credential);
    pipeline = LimesPipelineFactory.CreateAgents(factory, knowledge);
    Console.WriteLine($"Running in agents mode (Foundry: {connection.Endpoint}, deployment: {connection.Deployment}).");
}
else
{
    if (!string.IsNullOrWhiteSpace(parsed.KnowledgePath))
        Console.Error.WriteLine("Note: --knowledge is ignored in deterministic mode (grounding applies only to --mode agents).");
    pipeline = LimesPipelineFactory.CreateDeterministic();
}

var deliverable = await pipeline.RunAsync(intake, parsed.Mode);

var slug = string.Concat(deliverable.Assessment.Partner.Name.Where(char.IsLetterOrDigit)).ToLowerInvariant();
if (string.IsNullOrEmpty(slug)) slug = "partner";

var jsonContent = JsonReportWriter.Write(deliverable);
var mdContent = MarkdownReportWriter.Write(deliverable);
var jsonName = $"assessment-{slug}.json";
var mdName = $"assessment-{slug}.md";

string jsonLocation;
string mdLocation;
if (outputIsBlob)
{
    jsonLocation = (await RemoteIo.WriteAllTextAsync(parsed.OutputDir, jsonName, jsonContent, credential)).ToString();
    mdLocation = (await RemoteIo.WriteAllTextAsync(parsed.OutputDir, mdName, mdContent, credential)).ToString();
}
else
{
    jsonLocation = Path.Combine(parsed.OutputDir, jsonName);
    mdLocation = Path.Combine(parsed.OutputDir, mdName);
    await File.WriteAllTextAsync(jsonLocation, jsonContent);
    await File.WriteAllTextAsync(mdLocation, mdContent);
}

Console.WriteLine($"Limes assessment complete for '{deliverable.Assessment.Partner.Name}' ({deliverable.Mode} mode).");
Console.WriteLine($"  Readiness Index: {deliverable.Assessment.ReadinessIndex:0.00} / 5.00 ({deliverable.Assessment.OverallLevel})");
Console.WriteLine($"  Roadmap actions: {deliverable.Roadmap?.Actions.Count ?? 0}");
Console.WriteLine($"  Skilling recs:   {deliverable.SkillingPlan?.Recommendations.Count ?? 0}");
Console.WriteLine($"  Risks:           {deliverable.RiskRegister?.Risks.Count ?? 0}");
Console.WriteLine($"  JSON: {jsonLocation}");
Console.WriteLine($"  Markdown: {mdLocation}");

return 0;

/// <summary>Parsed command-line arguments for the orchestrator.</summary>
internal sealed record OrchestratorArgs
{
    public required string IntakePath { get; init; }
    public required string OutputDir { get; init; }
    public required AssessmentMode Mode { get; init; }
    public string? KnowledgePath { get; init; }

    public static OrchestratorArgs? Parse(string[] args)
    {
        string? intakePath = null;
        string? outputDir = null;
        var mode = AssessmentMode.Deterministic;
        var modeSpecified = false;
        string? knowledgePath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--mode":
                    if (++i >= args.Length || !TryParseMode(args[i], out mode))
                    {
                        Console.Error.WriteLine("--mode requires a value: deterministic | agents");
                        return null;
                    }
                    modeSpecified = true;
                    break;
                case "--knowledge":
                    if (++i >= args.Length)
                    {
                        Console.Error.WriteLine("--knowledge requires a file path.");
                        return null;
                    }
                    knowledgePath = args[i];
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine($"Unknown option: {arg}");
                        return null;
                    }
                    if (intakePath is null) intakePath = arg;
                    else if (outputDir is null) outputDir = arg;
                    else
                    {
                        Console.Error.WriteLine($"Unexpected argument: {arg}");
                        return null;
                    }
                    break;
            }
        }

        // Env fallbacks let the Container Apps Job run with no CLI args.
        intakePath ??= NullIfBlank(Environment.GetEnvironmentVariable("LIMES_INTAKE"));
        outputDir ??= NullIfBlank(Environment.GetEnvironmentVariable("LIMES_OUTPUT"));
        knowledgePath ??= NullIfBlank(Environment.GetEnvironmentVariable("LIMES_KNOWLEDGE"));
        if (!modeSpecified)
        {
            var envMode = Environment.GetEnvironmentVariable("LIMES_MODE");
            if (!string.IsNullOrWhiteSpace(envMode) && TryParseMode(envMode, out var em))
                mode = em;
        }

        if (intakePath is null)
        {
            Console.Error.WriteLine("Usage: Limes.Orchestrator <intake> [output] [--mode deterministic|agents] [--knowledge <path>]");
            Console.Error.WriteLine("  <intake>/[output] accept a local path or an Azure Blob URL (https://...).");
            Console.Error.WriteLine("  May also be supplied via LIMES_INTAKE / LIMES_OUTPUT / LIMES_MODE / LIMES_KNOWLEDGE.");
            return null;
        }

        return new OrchestratorArgs
        {
            IntakePath = intakePath,
            OutputDir = outputDir ?? Directory.GetCurrentDirectory(),
            Mode = mode,
            KnowledgePath = knowledgePath,
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool TryParseMode(string value, out AssessmentMode mode)
    {
        switch (value.ToLowerInvariant())
        {
            case "deterministic":
                mode = AssessmentMode.Deterministic;
                return true;
            case "agents":
                mode = AssessmentMode.Agents;
                return true;
            default:
                mode = AssessmentMode.Deterministic;
                return false;
        }
    }
}

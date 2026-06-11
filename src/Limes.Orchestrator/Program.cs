using Limes.Agents;
using Limes.Agents.Knowledge;
using Limes.Agents.Maf;
using Limes.Agents.Pipeline;
using Limes.Core.Domain;
using Limes.Core.Intake;
using Limes.Core.Reporting;

// Limes orchestrator — Phase 2.
// Usage: Limes.Orchestrator <intake.json> [outputDir] [--mode deterministic|agents] [--knowledge <path>]
//
// Runs the Janus → Iustitia → Providentia → Egeria → Terminus → Fama pipeline over a partner
// intake and writes assessment-<partner>.json and assessment-<partner>.md.
//
//   deterministic (default) — pure rules engine, $0 model cost, no Azure required.
//   agents                  — MAF + Foundry pipeline; requires LIMES_FOUNDRY_ENDPOINT and
//                             LIMES_FOUNDRY_DEPLOYMENT (auth via DefaultAzureCredential).

var parsed = OrchestratorArgs.Parse(args);
if (parsed is null)
    return 1;

if (!File.Exists(parsed.IntakePath))
{
    Console.Error.WriteLine($"Intake file not found: {parsed.IntakePath}");
    return 1;
}

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

var intake = await IntakeLoader.FromFileAsync(parsed.IntakePath);

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

    var factory = new FoundryAgentFactory(connection);
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

var jsonPath = Path.Combine(parsed.OutputDir, $"assessment-{slug}.json");
var mdPath = Path.Combine(parsed.OutputDir, $"assessment-{slug}.md");

await File.WriteAllTextAsync(jsonPath, JsonReportWriter.Write(deliverable));
await File.WriteAllTextAsync(mdPath, MarkdownReportWriter.Write(deliverable));

Console.WriteLine($"Limes assessment complete for '{deliverable.Assessment.Partner.Name}' ({deliverable.Mode} mode).");
Console.WriteLine($"  Readiness Index: {deliverable.Assessment.ReadinessIndex:0.00} / 5.00 ({deliverable.Assessment.OverallLevel})");
Console.WriteLine($"  Roadmap actions: {deliverable.Roadmap?.Actions.Count ?? 0}");
Console.WriteLine($"  Skilling recs:   {deliverable.SkillingPlan?.Recommendations.Count ?? 0}");
Console.WriteLine($"  Risks:           {deliverable.RiskRegister?.Risks.Count ?? 0}");
Console.WriteLine($"  JSON: {jsonPath}");
Console.WriteLine($"  Markdown: {mdPath}");

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

        if (intakePath is null)
        {
            Console.Error.WriteLine("Usage: Limes.Orchestrator <intake.json> [outputDir] [--mode deterministic|agents] [--knowledge <path>]");
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

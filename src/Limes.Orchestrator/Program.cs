using Limes.Core.Intake;
using Limes.Core.Reporting;
using Limes.Core.Scoring;

// Limes orchestrator — Phase 1 deterministic mode.
// Usage: Limes.Orchestrator <intake.json> [outputDir]
//
// Reads a partner intake JSON, runs the deterministic scoring engine ($0 model cost),
// and writes assessment-<partner>.json and assessment-<partner>.md to the output dir.
// The agents-mode pipeline (Janus → Iustitia → Providentia → Egeria → Terminus → Fama)
// will be layered on top of this same Limes.Core schema in Phase 2.

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: Limes.Orchestrator <intake.json> [outputDir]");
    return 1;
}

var intakePath = args[0];
var outputDir = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();

if (!File.Exists(intakePath))
{
    Console.Error.WriteLine($"Intake file not found: {intakePath}");
    return 1;
}

Directory.CreateDirectory(outputDir);

var intake = await IntakeLoader.FromFileAsync(intakePath);
var engine = new DeterministicScoringEngine();
var result = engine.Score(intake);

var slug = string.Concat(result.Partner.Name.Where(char.IsLetterOrDigit)).ToLowerInvariant();
if (string.IsNullOrEmpty(slug)) slug = "partner";

var jsonPath = Path.Combine(outputDir, $"assessment-{slug}.json");
var mdPath = Path.Combine(outputDir, $"assessment-{slug}.md");

await File.WriteAllTextAsync(jsonPath, JsonReportWriter.Write(result));
await File.WriteAllTextAsync(mdPath, MarkdownReportWriter.Write(result));

Console.WriteLine($"Limes assessment complete for '{result.Partner.Name}'.");
Console.WriteLine($"  Readiness Index: {result.ReadinessIndex:0.00} / 5.00 ({result.OverallLevel})");
Console.WriteLine($"  JSON: {jsonPath}");
Console.WriteLine($"  Markdown: {mdPath}");

return 0;

using Limes.Core.Domain;

namespace Limes.Agents.Pipeline;

/// <summary>
/// Mutable state threaded through the Limes agent pipeline. Each agent reads what earlier
/// stages produced and writes its own contribution. Janus normalizes <see cref="Intake"/>,
/// Iustitia sets <see cref="Scoring"/>, Providentia/Egeria/Terminus fill the plan sections,
/// and Fama assembles the final <see cref="Deliverable"/>.
/// </summary>
public sealed class AssessmentContext
{
    public required AssessmentIntake Intake { get; set; }
    public required AssessmentMode Mode { get; init; }

    public AssessmentResult? Scoring { get; set; }
    public Roadmap? Roadmap { get; set; }
    public SkillingPlan? SkillingPlan { get; set; }
    public RiskRegister? RiskRegister { get; set; }
    public AssessmentDeliverable? Deliverable { get; set; }

    /// <summary>Name + content hash of the grounding corpus, when an agent uses one.</summary>
    public string? KnowledgeSource { get; set; }

    private readonly List<string> _trace = [];
    public IReadOnlyList<string> Trace => _trace;

    public void Note(string codename, string message) => _trace.Add($"{codename}: {message}");

    /// <summary>Throws if a downstream agent runs before a required upstream stage produced output.</summary>
    public AssessmentResult RequireScoring() =>
        Scoring ?? throw new InvalidOperationException("Scoring is not available yet — Iustitia must run before this stage.");
}

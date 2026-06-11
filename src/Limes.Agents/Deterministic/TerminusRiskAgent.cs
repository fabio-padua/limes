using Limes.Agents.Pipeline;
using Limes.Agents.Reference;
using Limes.Core.Domain;

namespace Limes.Agents.Deterministic;

/// <summary>
/// Terminus (boundaries) — Risk &amp; Governance. Builds a risk register from the
/// risk-bearing pillars (Governance &amp; Security, Data Foundations, Model Management,
/// AI Strategy &amp; Experience). Severity is derived deterministically from the pillar score
/// and each risk is anchored to Responsible AI + WAF citations.
/// </summary>
public sealed class TerminusRiskAgent : ILimesAgent
{
    private static readonly IReadOnlyList<Pillar> RiskBearingPillars =
    [
        Pillar.GovernanceAndSecurity,
        Pillar.DataFoundations,
        Pillar.ModelManagement,
        Pillar.AiStrategyAndExperience,
    ];

    /// <summary>Pillars at or above this score do not raise a risk.</summary>
    public const double RiskThreshold = 4.0;

    public string Codename => "Terminus";
    public string Role => "Risk & Governance";

    public Task RunAsync(AssessmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scoring = context.RequireScoring();

        var byPillar = scoring.PillarScores.ToDictionary(p => p.Pillar);
        var risks = new List<Risk>();

        foreach (var pillar in RiskBearingPillars)
        {
            if (!byPillar.TryGetValue(pillar, out var score) || score.Score >= RiskThreshold)
                continue;

            risks.Add(new Risk
            {
                Id = $"RISK-{(int)pillar}",
                Pillar = pillar,
                Title = TitleFor(pillar),
                Description = $"{pillar.DisplayName()} maturity is {score.Level.DisplayName()} " +
                              $"({score.Score:0.00}/5.00), below the CoE target bar.",
                Severity = SeverityFor(score.Score),
                Mitigation = MitigationFor(pillar),
                Citations = AssessmentReference.Citations(pillar),
            });
        }

        var ordered = risks.OrderByDescending(r => r.Severity).ThenBy(r => (int)r.Pillar).ToList();
        context.RiskRegister = new RiskRegister { Risks = ordered };
        context.Note(Codename, $"registered {ordered.Count} risk(s).");

        return Task.CompletedTask;
    }

    private static RiskSeverity SeverityFor(double score) =>
        score < 2.0 ? RiskSeverity.Critical
        : score < 3.0 ? RiskSeverity.High
        : RiskSeverity.Medium;

    private static string TitleFor(Pillar pillar) => pillar switch
    {
        Pillar.GovernanceAndSecurity => "Insufficient Responsible AI governance and controls",
        Pillar.DataFoundations => "Weak data governance, quality, or compliance posture",
        Pillar.ModelManagement => "Immature model lifecycle and production monitoring",
        Pillar.AiStrategyAndExperience => "Ungoverned AI lifecycle and adoption roadmap",
        _ => $"{pillar.DisplayName()} maturity gap",
    };

    private static string MitigationFor(Pillar pillar) => pillar switch
    {
        Pillar.GovernanceAndSecurity => "Adopt the Responsible AI Standard; establish accountability, transparency, and risk controls.",
        Pillar.DataFoundations => "Stand up data governance with Microsoft Purview; enforce quality, privacy, and compliance gates.",
        Pillar.ModelManagement => "Introduce MLOps/LLMOps with versioning, evaluation gates, and production monitoring.",
        Pillar.AiStrategyAndExperience => "Document and govern the end-to-end AI lifecycle with monitoring and continuous improvement.",
        _ => "Align practices to CAF/WAF guidance.",
    };
}

using Limes.Agents.Pipeline;
using Limes.Agents.Reference;
using Limes.Core.Domain;

namespace Limes.Agents.Deterministic;

/// <summary>
/// Providentia (foresight) — Roadmap. Turns the scored gaps into a prioritized, wave-based,
/// dependency-aware remediation plan. Lower-maturity pillars are sequenced into earlier waves
/// as quick wins; foundational pillars (data, infrastructure, sponsorship) become prerequisites
/// for the pillars that depend on them. Every action carries CAF/WAF grounding citations.
/// </summary>
public sealed class ProvidentiaRoadmapAgent : ILimesAgent
{
    /// <summary>Pillars at or above this score are considered healthy and skip the roadmap.</summary>
    public const double TargetScore = 4.0;

    public string Codename => "Providentia";
    public string Role => "Roadmap";

    public Task RunAsync(AssessmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scoring = context.RequireScoring();

        // First pass: a wave/priority skeleton per pillar that needs work.
        var planned = scoring.PillarScores
            .Where(p => p.Score < TargetScore)
            .ToDictionary(
                p => p.Pillar,
                p => (Wave: WaveFor(p.Score), Priority: PriorityFor(p.Score), Pillar: p));

        var actions = new List<RemediationAction>();
        foreach (var (pillar, plan) in planned.OrderBy(kv => kv.Value.Wave).ThenBy(kv => (int)kv.Key))
        {
            var dependsOn = AssessmentReference.Prerequisites(pillar)
                .Where(pre => planned.TryGetValue(pre, out var preq) && preq.Wave <= plan.Wave && pre != pillar)
                .Select(ActionId)
                .ToList();

            actions.Add(new RemediationAction
            {
                Id = ActionId(pillar),
                Pillar = pillar,
                Title = $"Raise {pillar.DisplayName()} maturity ({plan.Pillar.Level.DisplayName()} → target)",
                Description = Describe(plan.Pillar),
                Wave = plan.Wave,
                Priority = plan.Priority,
                DependsOn = dependsOn,
                Citations = AssessmentReference.Citations(pillar),
            });
        }

        var ordered = actions.OrderBy(a => a.Wave).ThenBy(a => (int)a.Pillar).ToList();
        context.Roadmap = new Roadmap { Actions = ordered };
        context.Note(Codename,
            $"planned {ordered.Count} actions across {ordered.Select(a => a.Wave).DefaultIfEmpty(0).Distinct().Count()} wave(s).");

        return Task.CompletedTask;
    }

    private static int WaveFor(double score) => score < 2.0 ? 1 : score < 3.0 ? 2 : 3;

    private static ActionPriority PriorityFor(double score) =>
        score < 2.0 ? ActionPriority.QuickWin : ActionPriority.Strategic;

    private static string ActionId(Pillar pillar) => $"RA-{(int)pillar}";

    private static string Describe(PillarScore p)
    {
        if (p.Gaps.Count == 0)
            return $"Strengthen practices to move beyond {p.Level.DisplayName()} toward the CoE target bar.";

        var gaps = string.Join("; ", p.Gaps.Take(3));
        return $"Close the flagged gaps: {gaps}.";
    }
}

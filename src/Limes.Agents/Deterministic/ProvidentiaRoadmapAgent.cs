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
                p => (BaseWave: WaveFor(p.Score), Priority: PriorityFor(p.Score), Pillar: p));

        // Effective wave = at least the max wave of every in-scope prerequisite, so a dependent
        // action can never be scheduled before foundational work it explicitly depends on.
        var effectiveWaves = new Dictionary<Pillar, int>();
        foreach (var pillar in planned.Keys)
            ResolveWave(pillar, planned, effectiveWaves);

        var actions = new List<RemediationAction>();
        foreach (var (pillar, plan) in planned.OrderBy(kv => effectiveWaves[kv.Key]).ThenBy(kv => (int)kv.Key))
        {
            // Always wire prerequisites that are themselves in scope, regardless of their wave.
            var dependsOn = AssessmentReference.Prerequisites(pillar)
                .Where(pre => pre != pillar && planned.ContainsKey(pre))
                .Select(ActionId)
                .ToList();

            actions.Add(new RemediationAction
            {
                Id = ActionId(pillar),
                Pillar = pillar,
                Title = $"Raise {pillar.DisplayName()} maturity ({plan.Pillar.Level.DisplayName()} → target)",
                Description = Describe(plan.Pillar),
                Wave = effectiveWaves[pillar],
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

    /// <summary>
    /// Resolves a pillar's effective wave to the max of its own base wave and the effective waves
    /// of every in-scope prerequisite. Memoized; the seed-before-recurse pattern guards against
    /// cycles (the prerequisite graph is a DAG, but this stays safe if that ever changes).
    /// </summary>
    private static int ResolveWave(
        Pillar pillar,
        IReadOnlyDictionary<Pillar, (int BaseWave, ActionPriority Priority, PillarScore Pillar)> planned,
        Dictionary<Pillar, int> resolved)
    {
        if (resolved.TryGetValue(pillar, out var cached))
            return cached;

        var wave = planned[pillar].BaseWave;
        resolved[pillar] = wave;

        foreach (var pre in AssessmentReference.Prerequisites(pillar))
        {
            if (pre != pillar && planned.ContainsKey(pre))
                wave = Math.Max(wave, ResolveWave(pre, planned, resolved));
        }

        resolved[pillar] = wave;
        return wave;
    }

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

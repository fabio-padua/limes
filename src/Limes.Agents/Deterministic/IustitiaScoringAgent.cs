using Limes.Agents.Pipeline;
using Limes.Core.Domain;
using Limes.Core.Scoring;

namespace Limes.Agents.Deterministic;

/// <summary>
/// Iustitia (the scales) — Scoring &amp; Gap. Wraps the Phase 1
/// <see cref="DeterministicScoringEngine"/> to assign a 1-5 maturity score per pillar and flag
/// gaps. This is the deterministic fallback that the agents-mode scoring agent defers to, so
/// scores are always reproducible and never hallucinated.
/// </summary>
public sealed class IustitiaScoringAgent : ILimesAgent
{
    private readonly DeterministicScoringEngine _engine;

    public IustitiaScoringAgent(IReadOnlyDictionary<Pillar, double>? pillarWeights = null)
        => _engine = new DeterministicScoringEngine(pillarWeights);

    public string Codename => "Iustitia";
    public string Role => "Scoring & Gap";

    public Task RunAsync(AssessmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = _engine.Score(context.Intake);
        context.Scoring = result;
        context.Note(Codename,
            $"scored {result.PillarScores.Count} pillars; Readiness Index {result.ReadinessIndex:0.00}/5.00 ({result.OverallLevel}).");

        return Task.CompletedTask;
    }
}

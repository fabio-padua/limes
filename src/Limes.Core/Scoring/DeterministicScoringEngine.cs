using Limes.Core.Domain;

namespace Limes.Core.Scoring;

/// <summary>
/// Deterministic, $0-model-cost scoring engine (Phase 1 MVP). Computes a weighted
/// average per pillar, flags low-scoring questions as gaps, and rolls pillar scores
/// up into an overall Readiness Index. This is the testable, CI-friendly baseline;
/// the agents-mode pipeline layers reasoned narrative on top of the same schema.
/// </summary>
public sealed class DeterministicScoringEngine
{
    /// <summary>Questions scoring at or below this value are surfaced as gaps.</summary>
    public const int GapThreshold = 2;

    private readonly IReadOnlyDictionary<Pillar, double> _pillarWeights;

    public DeterministicScoringEngine(IReadOnlyDictionary<Pillar, double>? pillarWeights = null)
    {
        _pillarWeights = pillarWeights ?? PillarInfo.All.ToDictionary(p => p, _ => 1.0);
    }

    public AssessmentResult Score(AssessmentIntake intake)
    {
        ArgumentNullException.ThrowIfNull(intake);

        var pillarScores = intake.Pillars
            .Select(ScorePillar)
            .OrderBy(p => p.Pillar)
            .ToList();

        var readinessIndex = ComputeReadinessIndex(pillarScores);

        return new AssessmentResult
        {
            Partner = intake.Partner,
            PillarScores = pillarScores,
            ReadinessIndex = readinessIndex,
            OverallLevel = MaturityLevelInfo.FromScore(readinessIndex),
        };
    }

    private static PillarScore ScorePillar(PillarResponse pillar)
    {
        if (pillar.Responses.Count == 0)
        {
            return new PillarScore
            {
                Pillar = pillar.Pillar,
                Score = 1.0,
                Level = MaturityLevel.Initial,
                Gaps = ["No responses provided for this pillar."],
            };
        }

        var totalWeight = pillar.Responses.Sum(r => r.Weight);
        var weighted = totalWeight > 0
            ? pillar.Responses.Sum(r => ClampScore(r.Score) * r.Weight) / totalWeight
            : pillar.Responses.Average(r => ClampScore(r.Score));

        var gaps = pillar.Responses
            .Where(r => ClampScore(r.Score) <= GapThreshold)
            .Select(r => r.Prompt)
            .ToList();

        return new PillarScore
        {
            Pillar = pillar.Pillar,
            Score = Math.Round(weighted, 2),
            Level = MaturityLevelInfo.FromScore(weighted),
            Gaps = gaps,
        };
    }

    private double ComputeReadinessIndex(IReadOnlyList<PillarScore> pillarScores)
    {
        var totalWeight = pillarScores.Sum(p => _pillarWeights.GetValueOrDefault(p.Pillar, 1.0));
        if (totalWeight <= 0)
        {
            return Math.Round(pillarScores.Average(p => p.Score), 2);
        }

        var weighted = pillarScores.Sum(p => p.Score * _pillarWeights.GetValueOrDefault(p.Pillar, 1.0)) / totalWeight;
        return Math.Round(weighted, 2);
    }

    private static int ClampScore(int score) => Math.Clamp(score, 1, 5);
}

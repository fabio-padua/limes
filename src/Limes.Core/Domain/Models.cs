namespace Limes.Core.Domain;

/// <summary>Identifies the partner being assessed.</summary>
public sealed record PartnerProfile
{
    public required string Name { get; init; }
    public string? Region { get; init; }
    public string? Industry { get; init; }
}

/// <summary>
/// A single partner answer for one question within a pillar. <see cref="Score"/> is a
/// self-reported 1-5 maturity value; <see cref="Weight"/> lets some questions count more.
/// </summary>
public sealed record QuestionResponse
{
    public required string QuestionId { get; init; }
    public required string Prompt { get; init; }
    public required int Score { get; init; }
    public double Weight { get; init; } = 1.0;
    public string? Evidence { get; init; }
}

/// <summary>All question responses gathered for a single pillar.</summary>
public sealed record PillarResponse
{
    public required Pillar Pillar { get; init; }
    public required IReadOnlyList<QuestionResponse> Responses { get; init; }
}

/// <summary>The complete, normalized intake for one assessment run.</summary>
public sealed record AssessmentIntake
{
    public required PartnerProfile Partner { get; init; }
    public required IReadOnlyList<PillarResponse> Pillars { get; init; }
}

/// <summary>The computed result for one pillar.</summary>
public sealed record PillarScore
{
    public required Pillar Pillar { get; init; }
    public required double Score { get; init; }
    public required MaturityLevel Level { get; init; }
    public required IReadOnlyList<string> Gaps { get; init; }
}

/// <summary>The full assessment outcome: per-pillar scores plus the rolled-up index.</summary>
public sealed record AssessmentResult
{
    public required PartnerProfile Partner { get; init; }
    public required IReadOnlyList<PillarScore> PillarScores { get; init; }
    public required double ReadinessIndex { get; init; }
    public required MaturityLevel OverallLevel { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

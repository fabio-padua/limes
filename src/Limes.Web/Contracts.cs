using Limes.Core.Domain;

namespace Limes.Web;

/// <summary>The JSON shape returned to the browser after an assessment run. Enum values are
/// pre-resolved to their display names so the front end stays free of domain knowledge.</summary>
public sealed record AssessmentResponse
{
    public required string Id { get; init; }
    public required PartnerDto Partner { get; init; }
    public required string Mode { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required double ReadinessIndex { get; init; }
    public required string OverallLevel { get; init; }
    public required IReadOnlyList<PillarDto> Pillars { get; init; }
    public required IReadOnlyList<RoadmapItemDto> Roadmap { get; init; }
    public required IReadOnlyList<SkillingDto> Skilling { get; init; }
    public required IReadOnlyList<RiskDto> Risks { get; init; }
    public required IReadOnlyList<string> Trace { get; init; }
    public string? KnowledgeSource { get; init; }

    public static AssessmentResponse From(string id, AssessmentDeliverable d)
    {
        var a = d.Assessment;
        return new AssessmentResponse
        {
            Id = id,
            Partner = new PartnerDto(a.Partner.Name, a.Partner.Industry, a.Partner.Region),
            Mode = d.Mode.ToString(),
            GeneratedAtUtc = a.GeneratedAtUtc,
            ReadinessIndex = Math.Round(a.ReadinessIndex, 2),
            OverallLevel = a.OverallLevel.DisplayName(),
            Pillars = a.PillarScores
                .Select(p => new PillarDto(p.Pillar.DisplayName(), Math.Round(p.Score, 2), p.Level.DisplayName(), p.Gaps))
                .ToList(),
            Roadmap = (d.Roadmap?.Actions ?? [])
                .OrderBy(x => x.Wave).ThenBy(x => x.Pillar)
                .Select(x => new RoadmapItemDto(
                    x.Wave, x.Title, x.Pillar.DisplayName(), x.Priority.ToString(), x.Description, x.DependsOn, x.Citations))
                .ToList(),
            Skilling = (d.SkillingPlan?.Recommendations ?? [])
                .Select(r => new SkillingDto(r.Pillar.DisplayName(), r.Gap, r.LearnPath, r.Url, r.Role))
                .ToList(),
            Risks = (d.RiskRegister?.Risks ?? [])
                .OrderByDescending(r => r.Severity)
                .Select(r => new RiskDto(r.Severity.ToString(), r.Pillar.DisplayName(), r.Title, r.Description, r.Mitigation))
                .ToList(),
            Trace = d.PipelineTrace,
            KnowledgeSource = d.KnowledgeSource,
        };
    }
}

public sealed record PartnerDto(string Name, string? Industry, string? Region);
public sealed record PillarDto(string Pillar, double Score, string Level, IReadOnlyList<string> Gaps);
public sealed record RoadmapItemDto(
    int Wave, string Title, string Pillar, string Priority, string Description,
    IReadOnlyList<string> DependsOn, IReadOnlyList<string> Citations);
public sealed record SkillingDto(string Pillar, string Gap, string LearnPath, string? Url, string? Role);
public sealed record RiskDto(string Severity, string Pillar, string Title, string Description, string Mitigation);

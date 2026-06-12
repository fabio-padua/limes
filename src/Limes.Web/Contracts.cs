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

/// <summary>
/// The canonical questionnaire served to the guided survey UI. Built from
/// <see cref="Limes.Core.Intake.QuestionBank"/> so the survey, the file-drop intake, and the
/// CLI all share one set of questions. <see cref="QuestionnairePillarDto.Pillar"/> carries the
/// raw enum name the UI writes back into the assembled intake, while <c>DisplayName</c> is for
/// headings; <see cref="Levels"/> supplies the 1-5 maturity labels for the rating controls.
/// </summary>
public sealed record QuestionnaireDto(
    IReadOnlyList<QuestionnairePillarDto> Pillars,
    IReadOnlyList<MaturityLevelDto> Levels)
{
    public static QuestionnaireDto Build() => new(
        Limes.Core.Intake.QuestionBank.Pillars
            .Select(p => new QuestionnairePillarDto(
                p.Pillar.ToString(),
                p.Pillar.DisplayName(),
                p.Questions.Select(q => new QuestionDto(q.Id, q.Prompt)).ToList()))
            .ToList(),
        Enum.GetValues<MaturityLevel>()
            .OrderBy(l => (int)l)
            .Select(l => new MaturityLevelDto((int)l, l.DisplayName()))
            .ToList());
}

public sealed record QuestionnairePillarDto(string Pillar, string DisplayName, IReadOnlyList<QuestionDto> Questions);
public sealed record QuestionDto(string Id, string Prompt);
public sealed record MaturityLevelDto(int Value, string Label);

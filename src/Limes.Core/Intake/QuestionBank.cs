using Limes.Core.Domain;

namespace Limes.Core.Intake;

/// <summary>
/// A single curated survey question for one pillar. The <see cref="Id"/> matches the
/// <c>questionId</c> a respondent's answer carries into the intake, and <see cref="Prompt"/>
/// is the statement they rate on the 1-5 maturity scale.
/// </summary>
public sealed record SurveyQuestion
{
    public required string Id { get; init; }
    public required string Prompt { get; init; }

    /// <summary>Relative weight when averaging this pillar's score. Defaults to 1.0.</summary>
    public double Weight { get; init; } = 1.0;
}

/// <summary>The curated question set for one pillar.</summary>
public sealed record PillarQuestions
{
    public required Pillar Pillar { get; init; }
    public required IReadOnlyList<SurveyQuestion> Questions { get; init; }
}

/// <summary>
/// The canonical Limes questionnaire — the single source of truth for the questions a
/// respondent answers. The guided survey UI renders this bank, and each answer becomes a
/// <see cref="Domain.QuestionResponse"/> in the assembled intake, so the survey and the
/// file-drop intake share one schema. Questions mirror <c>samples/sample-intake.json</c>
/// so the bundled sample and the survey stay aligned.
/// </summary>
public static class QuestionBank
{
    /// <summary>All seven pillars and their curated questions, in canonical pillar order.</summary>
    public static readonly IReadOnlyList<PillarQuestions> Pillars =
    [
        new PillarQuestions
        {
            Pillar = Pillar.BusinessStrategy,
            Questions =
            [
                new() { Id = "BS1", Prompt = "AI initiatives are tied to measurable business value." },
                new() { Id = "BS2", Prompt = "Executive sponsorship and funding are in place." },
                new() { Id = "BS3", Prompt = "Use cases are prioritized against strategic goals." },
            ],
        },
        new PillarQuestions
        {
            Pillar = Pillar.AiStrategyAndExperience,
            Questions =
            [
                new() { Id = "AS1", Prompt = "A documented AI adoption roadmap exists." },
                new() { Id = "AS2", Prompt = "The AI lifecycle is governed end to end." },
            ],
        },
        new PillarQuestions
        {
            Pillar = Pillar.OrganizationAndCulture,
            Questions =
            [
                new() { Id = "OC1", Prompt = "AI roles and responsibilities are defined." },
                new() { Id = "OC2", Prompt = "A structured AI skilling plan is in place." },
            ],
        },
        new PillarQuestions
        {
            Pillar = Pillar.DataFoundations,
            Questions =
            [
                new() { Id = "DF1", Prompt = "Data governance and quality controls exist." },
                new() { Id = "DF2", Prompt = "Data security, privacy, and compliance are managed." },
            ],
        },
        new PillarQuestions
        {
            Pillar = Pillar.InfrastructureForAi,
            Questions =
            [
                new() { Id = "IN1", Prompt = "Scalable AI platform architecture (AI Foundry / WAF) is adopted." },
                new() { Id = "IN2", Prompt = "Infrastructure is provisioned as code." },
            ],
        },
        new PillarQuestions
        {
            Pillar = Pillar.ModelManagement,
            Questions =
            [
                new() { Id = "MM1", Prompt = "Model lifecycle and deployment are standardized (MLOps/LLMOps)." },
                new() { Id = "MM2", Prompt = "Models are monitored in production." },
            ],
        },
        new PillarQuestions
        {
            Pillar = Pillar.GovernanceAndSecurity,
            Questions =
            [
                new() { Id = "GS1", Prompt = "Responsible AI practices are documented and applied." },
                new() { Id = "GS2", Prompt = "AI risk management and controls are in place." },
            ],
        },
    ];

    /// <summary>Total number of questions across all pillars.</summary>
    public static int QuestionCount => Pillars.Sum(p => p.Questions.Count);
}

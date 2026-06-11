using Limes.Agents;
using Limes.Agents.Deterministic;
using Limes.Agents.Pipeline;
using Limes.Core.Domain;
using Domain = Limes.Core.Domain;

namespace Limes.Agents.Tests;

public class DeterministicPipelineTests
{
    private static PillarResponse Pillar(Pillar pillar, params int[] scores) => new()
    {
        Pillar = pillar,
        Responses = scores.Select((s, i) => new QuestionResponse
        {
            QuestionId = $"{pillar}-{i}",
            Prompt = $"Question {i} for {pillar}",
            Score = s,
        }).ToList(),
    };

    private static AssessmentIntake IntakeWith(params PillarResponse[] pillars) => new()
    {
        Partner = new PartnerProfile { Name = "Contoso", Region = "Brazil" },
        Pillars = pillars,
    };

    private static AssessmentIntake LowMaturityIntake() => IntakeWith(
        Pillar(Domain.Pillar.BusinessStrategy, 4, 3),
        Pillar(Domain.Pillar.AiStrategyAndExperience, 3, 2),
        Pillar(Domain.Pillar.OrganizationAndCulture, 2, 1),
        Pillar(Domain.Pillar.DataFoundations, 1, 2),
        Pillar(Domain.Pillar.InfrastructureForAi, 2, 2),
        Pillar(Domain.Pillar.ModelManagement, 2, 2),
        Pillar(Domain.Pillar.GovernanceAndSecurity, 3, 2));

    [Fact]
    public async Task FullPipeline_ProducesCompleteDeliverable()
    {
        var deliverable = await LimesPipelineFactory.CreateDeterministic()
            .RunAsync(LowMaturityIntake(), AssessmentMode.Deterministic);

        Assert.Equal(AssessmentMode.Deterministic, deliverable.Mode);
        Assert.Equal("Contoso", deliverable.Partner.Name);
        Assert.NotNull(deliverable.Roadmap);
        Assert.NotNull(deliverable.SkillingPlan);
        Assert.NotNull(deliverable.RiskRegister);
        Assert.NotEmpty(deliverable.Roadmap!.Actions);
        Assert.NotEmpty(deliverable.SkillingPlan!.Recommendations);
        Assert.NotEmpty(deliverable.RiskRegister!.Risks);
    }

    [Fact]
    public async Task FullPipeline_TraceRecordsEveryAgent()
    {
        var deliverable = await LimesPipelineFactory.CreateDeterministic()
            .RunAsync(LowMaturityIntake(), AssessmentMode.Deterministic);

        foreach (var codename in new[] { "Janus", "Iustitia", "Providentia", "Egeria", "Terminus", "Fama" })
            Assert.Contains(deliverable.PipelineTrace, line => line.StartsWith(codename, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Janus_EnsuresAllSevenPillarsAreScored()
    {
        // Only one pillar supplied; Janus should add the other six as empty.
        var deliverable = await LimesPipelineFactory.CreateDeterministic()
            .RunAsync(IntakeWith(Pillar(Domain.Pillar.BusinessStrategy, 4)), AssessmentMode.Deterministic);

        Assert.Equal(PillarInfo.All.Count, deliverable.Assessment.PillarScores.Count);
    }

    [Fact]
    public async Task Janus_DropsDuplicateQuestionIds()
    {
        var ctx = new AssessmentContext
        {
            Intake = IntakeWith(new PillarResponse
            {
                Pillar = Domain.Pillar.DataFoundations,
                Responses =
                [
                    new QuestionResponse { QuestionId = "DUP", Prompt = "first", Score = 4 },
                    new QuestionResponse { QuestionId = "DUP", Prompt = "second", Score = 1 },
                ],
            }),
            Mode = AssessmentMode.Deterministic,
        };

        await new JanusIntakeAgent().RunAsync(ctx);

        var data = ctx.Intake.Pillars.Single(p => p.Pillar == Domain.Pillar.DataFoundations);
        var response = Assert.Single(data.Responses);
        Assert.Equal("first", response.Prompt);
    }

    [Fact]
    public async Task Janus_ToleratesDuplicatePillarBlocks_KeepingFirst()
    {
        // Two blocks for the same pillar — Janus must not throw and should keep the first.
        var ctx = new AssessmentContext
        {
            Intake = IntakeWith(
                new PillarResponse
                {
                    Pillar = Domain.Pillar.DataFoundations,
                    Responses = [new QuestionResponse { QuestionId = "A", Prompt = "first-block", Score = 4 }],
                },
                new PillarResponse
                {
                    Pillar = Domain.Pillar.DataFoundations,
                    Responses = [new QuestionResponse { QuestionId = "B", Prompt = "second-block", Score = 1 }],
                }),
            Mode = AssessmentMode.Deterministic,
        };

        await new JanusIntakeAgent().RunAsync(ctx);

        var data = ctx.Intake.Pillars.Single(p => p.Pillar == Domain.Pillar.DataFoundations);
        var response = Assert.Single(data.Responses);
        Assert.Equal("first-block", response.Prompt);
        Assert.Equal(PillarInfo.All.Count, ctx.Intake.Pillars.Count);
    }

    [Fact]
    public async Task Providentia_SkipsHealthyPillars()
    {
        // Every pillar at 5/5 → no remediation actions.
        var allHealthy = IntakeWith(PillarInfo.All.Select(p => Pillar(p, 5, 5)).ToArray());
        var deliverable = await LimesPipelineFactory.CreateDeterministic()
            .RunAsync(allHealthy, AssessmentMode.Deterministic);

        Assert.Empty(deliverable.Roadmap!.Actions);
        Assert.Empty(deliverable.RiskRegister!.Risks);
    }

    [Fact]
    public async Task Providentia_WiresDataFoundationsAsPrerequisiteForModelManagement()
    {
        var deliverable = await LimesPipelineFactory.CreateDeterministic()
            .RunAsync(LowMaturityIntake(), AssessmentMode.Deterministic);

        var modelAction = deliverable.Roadmap!.Actions
            .Single(a => a.Pillar == Domain.Pillar.ModelManagement);
        var dataActionId = deliverable.Roadmap!.Actions
            .Single(a => a.Pillar == Domain.Pillar.DataFoundations).Id;

        Assert.Contains(dataActionId, modelAction.DependsOn);
    }

    [Fact]
    public async Task Providentia_OrdersActionsByWave()
    {
        var deliverable = await LimesPipelineFactory.CreateDeterministic()
            .RunAsync(LowMaturityIntake(), AssessmentMode.Deterministic);

        var waves = deliverable.Roadmap!.Actions.Select(a => a.Wave).ToList();
        Assert.Equal(waves.OrderBy(w => w), waves);
    }

    [Fact]
    public async Task Terminus_RaisesCriticalRiskForLowestPillars()
    {
        var deliverable = await LimesPipelineFactory.CreateDeterministic()
            .RunAsync(LowMaturityIntake(), AssessmentMode.Deterministic);

        // DataFoundations averages 1.5 → Critical.
        var dataRisk = deliverable.RiskRegister!.Risks
            .Single(r => r.Pillar == Domain.Pillar.DataFoundations);
        Assert.Equal(RiskSeverity.Critical, dataRisk.Severity);
    }

    [Fact]
    public async Task Pipeline_ThrowsWhenFamaNeverRuns()
    {
        var pipeline = new LimesPipeline([new JanusIntakeAgent()]);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline.RunAsync(LowMaturityIntake(), AssessmentMode.Deterministic));
    }
}

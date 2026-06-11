using Limes.Core.Domain;
using Limes.Core.Scoring;
using Xunit;

namespace Limes.Core.Tests;

public class DeterministicScoringEngineTests
{
    private static PillarResponse Pillar(Pillar pillar, params int[] scores) => new()
    {
        Pillar = pillar,
        Responses = scores.Select((s, i) => new QuestionResponse
        {
            QuestionId = $"{pillar}-{i}",
            Prompt = $"Question {i}",
            Score = s,
        }).ToList(),
    };

    private static AssessmentIntake IntakeWith(params PillarResponse[] pillars) => new()
    {
        Partner = new PartnerProfile { Name = "Test Partner" },
        Pillars = pillars,
    };

    [Fact]
    public void Score_AveragesQuestionsWithinPillar()
    {
        var intake = IntakeWith(Pillar(Domain.Pillar.BusinessStrategy, 2, 4));
        var result = new DeterministicScoringEngine().Score(intake);

        var pillar = Assert.Single(result.PillarScores);
        Assert.Equal(3.0, pillar.Score);
        Assert.Equal(MaturityLevel.Defined, pillar.Level);
    }

    [Fact]
    public void Score_RespectsQuestionWeights()
    {
        var intake = new AssessmentIntake
        {
            Partner = new PartnerProfile { Name = "Test Partner" },
            Pillars =
            [
                new PillarResponse
                {
                    Pillar = Domain.Pillar.DataFoundations,
                    Responses =
                    [
                        new QuestionResponse { QuestionId = "a", Prompt = "a", Score = 5, Weight = 3 },
                        new QuestionResponse { QuestionId = "b", Prompt = "b", Score = 1, Weight = 1 },
                    ],
                },
            ],
        };

        var result = new DeterministicScoringEngine().Score(intake);
        // (5*3 + 1*1) / 4 = 4.0
        Assert.Equal(4.0, result.PillarScores[0].Score);
    }

    [Fact]
    public void Score_FlagsLowScoringQuestionsAsGaps()
    {
        var intake = IntakeWith(Pillar(Domain.Pillar.ModelManagement, 1, 2, 5));
        var result = new DeterministicScoringEngine().Score(intake);

        var pillar = Assert.Single(result.PillarScores);
        Assert.Equal(2, pillar.Gaps.Count);
    }

    [Fact]
    public void Score_ClampsOutOfRangeValues()
    {
        var intake = IntakeWith(Pillar(Domain.Pillar.InfrastructureForAi, 9, -3));
        var result = new DeterministicScoringEngine().Score(intake);

        // clamps to 5 and 1 -> average 3.0
        Assert.Equal(3.0, result.PillarScores[0].Score);
    }

    [Fact]
    public void Score_EmptyPillarIsInitial()
    {
        var intake = IntakeWith(new PillarResponse
        {
            Pillar = Domain.Pillar.GovernanceAndSecurity,
            Responses = [],
        });

        var result = new DeterministicScoringEngine().Score(intake);
        Assert.Equal(MaturityLevel.Initial, result.PillarScores[0].Level);
        Assert.Single(result.PillarScores[0].Gaps);
    }

    [Fact]
    public void Score_AppliesPillarWeightsToReadinessIndex()
    {
        var intake = IntakeWith(
            Pillar(Domain.Pillar.BusinessStrategy, 5),
            Pillar(Domain.Pillar.ModelManagement, 1));

        var weights = new Dictionary<Pillar, double>
        {
            [Domain.Pillar.BusinessStrategy] = 3,
            [Domain.Pillar.ModelManagement] = 1,
        };

        var result = new DeterministicScoringEngine(weights).Score(intake);
        // (5*3 + 1*1) / 4 = 4.0
        Assert.Equal(4.0, result.ReadinessIndex);
    }
}

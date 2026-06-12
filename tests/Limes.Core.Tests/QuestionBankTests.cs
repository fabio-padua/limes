using Limes.Core.Domain;
using Limes.Core.Intake;

namespace Limes.Core.Tests;

public sealed class QuestionBankTests
{
    [Fact]
    public void Pillars_CoverAllSevenReadinessPillars()
    {
        var covered = QuestionBank.Pillars.Select(p => p.Pillar).ToHashSet();
        Assert.Equal(Enum.GetValues<Pillar>().ToHashSet(), covered);
    }

    [Fact]
    public void EveryPillar_HasAtLeastOneQuestion()
    {
        Assert.All(QuestionBank.Pillars, p => Assert.NotEmpty(p.Questions));
    }

    [Fact]
    public void QuestionIds_AreUniqueNonEmptyAndCountMatches()
    {
        var ids = QuestionBank.Pillars.SelectMany(p => p.Questions.Select(q => q.Id)).ToList();

        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(ids.Count, QuestionBank.QuestionCount);
    }

    [Fact]
    public void EveryQuestion_HasAPrompt()
    {
        var prompts = QuestionBank.Pillars.SelectMany(p => p.Questions.Select(q => q.Prompt));
        Assert.All(prompts, prompt => Assert.False(string.IsNullOrWhiteSpace(prompt)));
    }
}

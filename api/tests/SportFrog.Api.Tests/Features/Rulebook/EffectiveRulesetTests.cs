using SportFrog.Api.Features.Rulebook;

namespace SportFrog.Api.Tests.Features.Rulebook;

/// <summary>
/// <see cref="EffectiveRuleset.Of"/>: the override where a category has one,
/// the competition's otherwise.
/// </summary>
public sealed class EffectiveRulesetTests
{
    [Fact]
    public void Of_CategoryHasNoOverride_ReturnsTheCompetitionsRuleset()
    {
        var competitionRulesetId = Guid.NewGuid();

        Assert.Equal(competitionRulesetId, EffectiveRuleset.Of(null, competitionRulesetId));
    }

    [Fact]
    public void Of_CategoryOverrides_ReturnsTheCategorysOwnRuleset()
    {
        var categoryRulesetId = Guid.NewGuid();

        Assert.Equal(categoryRulesetId, EffectiveRuleset.Of(categoryRulesetId, Guid.NewGuid()));
    }
}

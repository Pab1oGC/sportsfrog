using AwesomeAssertions;
using SportFrog.Domain.Rules;

namespace SportFrog.Domain.Tests.Rules;

/// <summary>
/// The set of outcomes a ruleset must price, and the key one finished result
/// maps to. Expected keys below are worked out by hand from "a best-of-N
/// match ends on one of the N/2-rounded-up scorelines, from either side", not
/// copied from whatever the private formatting loop currently emits.
/// </summary>
public sealed class MatchOutcomesTests
{
    // ---- Cumulative --------------------------------------------------

    [Fact]
    public void RequiredFor_Cumulative_IsWinAndLossOnly()
    {
        // A draw is never required — whether a cumulative match can end level
        // is a competition choice, not a sport one.
        MatchOutcomes.RequiredFor(ScoreMode.Cumulative, periods: 2)
            .Should().BeEquivalentTo(["win", "loss"]);
    }

    [Fact]
    public void OptionalFor_Cumulative_OffersOnlyDraw()
    {
        MatchOutcomes.OptionalFor(ScoreMode.Cumulative).Should().BeEquivalentTo(["draw"]);
    }

    [Theory]
    [InlineData(2, 1, "win")]
    [InlineData(1, 2, "loss")]
    [InlineData(1, 1, "draw")]
    [InlineData(0, 0, "draw")]
    public void For_Cumulative_ComparesOwnAgainstOpponent(int own, int against, string expected)
    {
        MatchOutcomes.For(ScoreMode.Cumulative, own, against).Should().Be(expected);
    }

    // ---- Sets ------------------------------------------------------------

    [Fact]
    public void RequiredFor_Sets_BestOfThree_IsEveryFinishingScorelineBothWays()
    {
        // Best of three is won at two. A match can finish 2-0 or 2-1, and
        // each of those has to be priced from both sides.
        MatchOutcomes.RequiredFor(ScoreMode.Sets, periods: 3)
            .Should().BeEquivalentTo(["win_2_0", "loss_0_2", "win_2_1", "loss_1_2"]);
    }

    [Fact]
    public void RequiredFor_Sets_BestOfFive_IsEveryFinishingScorelineBothWays()
    {
        // Best of five is won at three: 3-0, 3-1 or 3-2, each priced from
        // both sides — six keys in total.
        MatchOutcomes.RequiredFor(ScoreMode.Sets, periods: 5)
            .Should().BeEquivalentTo(
                ["win_3_0", "loss_0_3", "win_3_1", "loss_1_3", "win_3_2", "loss_2_3"]);
    }

    [Fact]
    public void RequiredFor_Sets_BestOfSeven_IsEveryFinishingScorelineBothWays()
    {
        // Best of seven is won at four: 4-0, 4-1, 4-2 or 4-3.
        MatchOutcomes.RequiredFor(ScoreMode.Sets, periods: 7)
            .Should().BeEquivalentTo(
                [
                    "win_4_0", "loss_0_4",
                    "win_4_1", "loss_1_4",
                    "win_4_2", "loss_2_4",
                    "win_4_3", "loss_3_4",
                ]);
    }

    [Fact]
    public void OptionalFor_Sets_OffersNothing()
    {
        // A match played in sets runs until someone takes the deciding one:
        // there is no draw to price.
        MatchOutcomes.OptionalFor(ScoreMode.Sets).Should().BeEmpty();
    }

    [Theory]
    [InlineData(3, 0, "win_3_0")]
    [InlineData(3, 1, "win_3_1")]
    [InlineData(3, 2, "win_3_2")]
    [InlineData(0, 3, "loss_0_3")]
    [InlineData(1, 3, "loss_1_3")]
    [InlineData(2, 3, "loss_2_3")]
    public void For_Sets_FormatsAsResultUnderscoreOwnUnderscoreAgainst(int own, int against, string expected)
    {
        MatchOutcomes.For(ScoreMode.Sets, own, against).Should().Be(expected);
    }

    [Fact]
    public void RequiredFor_Sets_AndFor_UseTheExactSameKeys()
    {
        // The contract MatchOutcomes documents for itself: a key For() can
        // produce for a real finished score must be one RequiredFor() also
        // asked the ruleset to price, for every scoreline a best-of-five
        // match can end on. A single character of drift between the two
        // would make a real result silently worth zero.
        var required = MatchOutcomes.RequiredFor(ScoreMode.Sets, periods: 5);

        var toWin = 3;
        var produced = new List<string>();
        for (var lost = 0; lost < toWin; lost++)
        {
            produced.Add(MatchOutcomes.For(ScoreMode.Sets, toWin, lost));
            produced.Add(MatchOutcomes.For(ScoreMode.Sets, lost, toWin));
        }

        produced.Should().BeEquivalentTo(required);
    }

    // ---- Judged ------------------------------------------------------

    [Fact]
    public void RequiredFor_Judged_IsWinAndLossOnly()
    {
        MatchOutcomes.RequiredFor(ScoreMode.Judged, periods: 1)
            .Should().BeEquivalentTo(["win", "loss"]);
    }

    [Fact]
    public void OptionalFor_Judged_OffersNothing()
    {
        // Unlike a cumulative match, whether a judged bout may end level is
        // not left to the competition: judges settle it before a result
        // ever reaches here, so there is no draw to offer even as optional.
        MatchOutcomes.OptionalFor(ScoreMode.Judged).Should().BeEmpty();
    }

    [Theory]
    [InlineData(765, 742, "win")]
    [InlineData(742, 765, "loss")]
    public void For_Judged_ComparesOwnAgainstOpponent(int own, int against, string expected)
    {
        MatchOutcomes.For(ScoreMode.Judged, own, against).Should().Be(expected);
    }
}

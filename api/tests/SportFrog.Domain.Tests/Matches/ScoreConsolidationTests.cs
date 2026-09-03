using AwesomeAssertions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Domain.Tests.Matches;

/// <summary>
/// Cumulative sports sum what was scored; sports played in sets count how
/// many periods each side took. Expected values below are worked out by hand
/// from that rule, not read off whatever the implementation currently
/// returns.
/// </summary>
public sealed class ScoreConsolidationTests
{
    private static PeriodScore Period(short number, int home, int away) =>
        new() { Period = number, Home = home, Away = away };

    // ---- Cumulative ----------------------------------------------------

    [Fact]
    public void Consolidate_Cumulative_SumsEachSideAcrossPeriods()
    {
        // A football match: 1-0 in the first half, 2-1 in the second.
        // Full time is the total of both: 3-1.
        var periods = new[] { Period(1, 1, 0), Period(2, 2, 1) };

        var (home, away) = ScoreConsolidation.Consolidate(ScoreMode.Cumulative, periods);

        home.Should().Be(3);
        away.Should().Be(1);
    }

    [Fact]
    public void Consolidate_Cumulative_OneSideScorelessThroughout_StaysZero()
    {
        var periods = new[] { Period(1, 4, 0), Period(2, 3, 0) };

        var (home, away) = ScoreConsolidation.Consolidate(ScoreMode.Cumulative, periods);

        home.Should().Be(7);
        away.Should().Be(0);
    }

    [Fact]
    public void Consolidate_Cumulative_NoPeriodsReported_IsScoreless()
    {
        // The function itself does not require at least one period — that
        // guard lives elsewhere. Given nothing, a sum of nothing is zero.
        var (home, away) = ScoreConsolidation.Consolidate(ScoreMode.Cumulative, []);

        home.Should().Be(0);
        away.Should().Be(0);
    }

    // ---- Sets ------------------------------------------------------------

    [Fact]
    public void Consolidate_Sets_CountsPeriodsWonNotPointsScored()
    {
        // A volleyball match of 25-20, 22-25, 25-18: two sets to one, and the
        // 72 raw points across all three sets are not the result of anything.
        var periods = new[] { Period(1, 25, 20), Period(2, 22, 25), Period(3, 25, 18) };

        var (home, away) = ScoreConsolidation.Consolidate(ScoreMode.Sets, periods);

        home.Should().Be(2);
        away.Should().Be(1);
    }

    [Fact]
    public void Consolidate_Sets_SweepIsAllPeriodsToTheSameSide()
    {
        // Best of five, won three sets to none.
        var periods = new[] { Period(1, 25, 10), Period(2, 25, 15), Period(3, 25, 20) };

        var (home, away) = ScoreConsolidation.Consolidate(ScoreMode.Sets, periods);

        home.Should().Be(3);
        away.Should().Be(0);
    }

    [Fact]
    public void Consolidate_Sets_ATiedPeriod_CountsForNeitherSide()
    {
        // Consolidate does not validate — that is ResultPolicy's job. Given a
        // period nobody won outright, it is honest about not crediting
        // either side rather than guessing.
        var periods = new[] { Period(1, 25, 23), Period(2, 20, 20) };

        var (home, away) = ScoreConsolidation.Consolidate(ScoreMode.Sets, periods);

        home.Should().Be(1);
        away.Should().Be(0);
    }

    // ---- PeriodsToWin ------------------------------------------------------

    [Theory]
    [InlineData(5, 3)] // Best of five is won at three.
    [InlineData(3, 2)] // Best of three is won at two.
    [InlineData(1, 1)] // A single, deciding period.
    [InlineData(7, 4)] // Best of seven is won at four.
    public void PeriodsToWin_MatchesTheOddBestOfCountItIsConfiguredFor(short configured, int expected)
    {
        ScoreConsolidation.PeriodsToWin(configured).Should().Be(expected);
    }
}

using AwesomeAssertions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Domain.Tests.Matches;

/// <summary>
/// The running score of a match still in progress, tallied from whatever
/// events have been recorded so far.
/// </summary>
public sealed class LiveScoreTests
{
    [Fact]
    public void AppliesTo_Cumulative_IsTrue()
    {
        // A goal moves the scoreboard the instant it is recorded — a live
        // score is exactly what that adds up to.
        LiveScore.AppliesTo(ScoreMode.Cumulative).Should().BeTrue();
    }

    [Fact]
    public void AppliesTo_Sets_IsFalse()
    {
        // Nothing recorded during a set moves the match score — it comes
        // from periods won, decided only once the periods are reported —
        // so there is no running total for a live score to be.
        LiveScore.AppliesTo(ScoreMode.Sets).Should().BeFalse();
    }


    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Away = Guid.NewGuid();

    private static ScoringEvent Goal(Guid teamId, int quantity = 1) =>
        new(teamId, ScorePoints: 1, CountsForOpponent: false, quantity);

    [Fact]
    public void Compute_NoEvents_IsScoreless()
    {
        var totals = LiveScore.Compute([], Home, Away);

        totals.Should().Be(new LiveScore.Totals(0, 0));
    }

    [Fact]
    public void Compute_OneGoalEach_TalliesBothSides()
    {
        var totals = LiveScore.Compute([Goal(Home), Goal(Away)], Home, Away);

        totals.Should().Be(new LiveScore.Totals(1, 1));
    }

    [Fact]
    public void Compute_OwnGoal_CreditsTheOtherSide()
    {
        // Recorded against the home roster, but it is an own goal: the point
        // belongs to the visitor.
        var ownGoal = new ScoringEvent(Home, ScorePoints: 1, CountsForOpponent: true, Quantity: 1);

        var totals = LiveScore.Compute([ownGoal], Home, Away);

        totals.Should().Be(new LiveScore.Totals(0, 1));
    }

    [Fact]
    public void Compute_WeightsByScorePoints()
    {
        // A basketball three-pointer and a free throw, both by the home side.
        var threePointer = new ScoringEvent(Home, ScorePoints: 3, CountsForOpponent: false, Quantity: 1);
        var freeThrow = new ScoringEvent(Home, ScorePoints: 1, CountsForOpponent: false, Quantity: 1);

        var totals = LiveScore.Compute([threePointer, freeThrow], Home, Away);

        totals.Should().Be(new LiveScore.Totals(4, 0));
    }

    [Fact]
    public void Compute_MultipliesByQuantity()
    {
        // Three free throws logged in one entry.
        var freeThrows = new ScoringEvent(Away, ScorePoints: 1, CountsForOpponent: false, Quantity: 3);

        var totals = LiveScore.Compute([freeThrows], Home, Away);

        totals.Should().Be(new LiveScore.Totals(0, 3));
    }

    [Fact]
    public void Compute_TeamThatIsNeitherSide_IsNotCounted()
    {
        var totals = LiveScore.Compute([Goal(Guid.NewGuid())], Home, Away);

        totals.Should().Be(new LiveScore.Totals(0, 0));
    }

    [Fact]
    public void ComputePeriods_SplitsEventsByTheirPeriod()
    {
        var events = new[]
        {
            new ScoringEvent(Home, 1, false, 1, PeriodNumber: 1),
            new ScoringEvent(Home, 1, false, 1, PeriodNumber: 2),
            new ScoringEvent(Away, 1, false, 1, PeriodNumber: 2),
        };

        var periods = LiveScore.ComputePeriods(events, periodCount: 2, Home, Away);

        periods.Should().BeEquivalentTo(
        [
            new PeriodScore { Period = 1, Home = 1, Away = 0 },
            new PeriodScore { Period = 2, Home = 1, Away = 1 },
        ]);
    }

    [Fact]
    public void ComputePeriods_EventWithNoPeriod_FallsIntoTheFirstOne()
    {
        var events = new[] { new ScoringEvent(Away, 1, false, 1, PeriodNumber: null) };

        var periods = LiveScore.ComputePeriods(events, periodCount: 2, Home, Away);

        periods.Should().BeEquivalentTo(
        [
            new PeriodScore { Period = 1, Home = 0, Away = 1 },
            new PeriodScore { Period = 2, Home = 0, Away = 0 },
        ]);
    }

    [Fact]
    public void ComputePeriods_EventWithAPeriodOutsideRange_FallsIntoTheFirstOneInsteadOfBeingLost()
    {
        var events = new[] { new ScoringEvent(Home, 1, false, 1, PeriodNumber: 7) };

        var periods = LiveScore.ComputePeriods(events, periodCount: 2, Home, Away);

        periods.Sum(period => period.Home).Should().Be(1);
        periods.Single(period => period.Period == 1).Home.Should().Be(1);
    }

    [Fact]
    public void ComputePeriods_ProducesExactlyOneEntryPerConfiguredPeriod()
    {
        var periods = LiveScore.ComputePeriods([], periodCount: 4, Home, Away);

        periods.Select(period => period.Period).Should().BeEquivalentTo((short[])[1, 2, 3, 4]);
        periods.Should().OnlyContain(period => period.Home == 0 && period.Away == 0);
    }
}

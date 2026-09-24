using AwesomeAssertions;
using SportFrog.Domain.Rules;

namespace SportFrog.Domain.Tests.Rules;

/// <summary>
/// Which minutes of the match clock a period may carry. Expected values come
/// from how a football match is read out — the second half starts at 46, and
/// 48 in the first half is 45+3 — not from PeriodClock's own arithmetic.
/// </summary>
public sealed class PeriodClockTests
{
    private static PeriodRules Halves(short minutes = 45, short? extra = null) => new()
    {
        Count = 2,
        Label = "tiempo",
        Minutes = minutes,
        MaxExtraMinutes = extra,
    };

    [Fact]
    public void MinuteWindow_FirstHalf_RunsFromKickoffToTheEndPlusStoppageTime()
    {
        PeriodClock.MinuteWindow(Halves(), 1).Should().Be((0, 65));
    }

    [Fact]
    public void MinuteWindow_SecondHalf_StartsOnePastTheFirstHalfAndEndsAtNinetyPlusStoppage()
    {
        PeriodClock.MinuteWindow(Halves(), 2).Should().Be((46, 110));
    }

    [Fact]
    public void MinuteWindow_MinuteNinetyNine_IsNotReachableFromTheFirstHalf()
    {
        // The case that started this: filing minute 99 under the first half.
        var window = PeriodClock.MinuteWindow(Halves(), 1)!.Value;

        99.Should().BeGreaterThan(window.To);
    }

    [Fact]
    public void MinuteWindow_TheFirstHalfsStoppageTime_IsNotTheSecondHalfsToClaim()
    {
        // 45+3 is minute 48 in the first half; the second half does not open
        // until 46, so 48 there is a different (later) moment entirely.
        var first = PeriodClock.MinuteWindow(Halves(), 1)!.Value;
        var second = PeriodClock.MinuteWindow(Halves(), 2)!.Value;

        second.From.Should().Be(46);
        first.To.Should().BeGreaterThan(second.From);
    }

    [Fact]
    public void MinuteWindow_FollowsTheReglamentosOwnPeriodLength()
    {
        // Youth football in 30-minute halves.
        var halves = Halves(30);

        PeriodClock.MinuteWindow(halves, 1).Should().Be((0, 50));
        PeriodClock.MinuteWindow(halves, 2).Should().Be((31, 80));
    }

    [Fact]
    public void MinuteWindow_UsesTheReglamentosOwnStoppageAllowance()
    {
        PeriodClock.MinuteWindow(Halves(extra: 10), 1).Should().Be((0, 55));
        PeriodClock.MinuteWindow(Halves(extra: 0), 2).Should().Be((46, 90));
    }

    [Fact]
    public void MinuteWindow_QuartersOfBasketball_ChainOnePastTheLast()
    {
        var quarters = new PeriodRules { Count = 4, Label = "cuarto", Minutes = 10, MaxExtraMinutes = 5 };

        PeriodClock.MinuteWindow(quarters, 3).Should().Be((21, 35));
    }

    [Fact]
    public void MinuteWindow_AnythingWithoutAClockOrOutsideThePeriods_HasNoAnswer()
    {
        var noClock = new PeriodRules { Count = 3, Label = "set", Minutes = null, EstimatedMinutes = 30 };

        PeriodClock.MinuteWindow(noClock, 1).Should().BeNull();
        PeriodClock.MinuteWindow(Halves(), 0).Should().BeNull();
        PeriodClock.MinuteWindow(Halves(), 3).Should().BeNull();
    }

    [Fact]
    public void LastMinute_IsTheEndOfTheLastPeriodPlusItsStoppageTime()
    {
        PeriodClock.LastMinute(Halves()).Should().Be(110);
        PeriodClock.LastMinute(new PeriodRules { Count = 3, Label = "set", Minutes = null }).Should().BeNull();
    }
}

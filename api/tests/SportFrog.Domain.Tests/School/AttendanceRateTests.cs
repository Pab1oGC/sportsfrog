using AwesomeAssertions;
using SportFrog.Domain.School;

namespace SportFrog.Domain.Tests.School;

/// <summary>
/// What a rate is built from. The one rule every test here circles back to:
/// a session only counts if it was held <em>and</em> somebody marked it —
/// see <see cref="AttendanceRate"/>'s own remarks for why both halves of
/// that are load-bearing.
/// </summary>
public sealed class AttendanceRateTests
{
    private static AttendanceEntry Marked(AttendanceState state) => new(Held: true, Mark: state);
    private static readonly AttendanceEntry NotHeld = new(Held: false, Mark: null);
    private static readonly AttendanceEntry HeldButUnmarked = new(Held: true, Mark: null);

    [Fact]
    public void Resolve_NoEntries_HasNoRateRatherThanAZeroOne()
    {
        var tally = AttendanceRate.Resolve([]);

        tally.Recorded.Should().Be(0);
        tally.Rate.Should().BeNull();
    }

    [Fact]
    public void Resolve_OnlyCancelledOrFutureSessions_HasNoRate()
    {
        // Neither asked anything of the student, so neither is evidence of
        // anything — not even a denominator of zero attendance out of zero.
        var tally = AttendanceRate.Resolve([NotHeld, NotHeld, NotHeld]);

        tally.Recorded.Should().Be(0);
        tally.Rate.Should().BeNull();
    }

    [Fact]
    public void Resolve_HeldSessionNobodyMarked_IsExcludedNotCountedAsAbsent()
    {
        // The gap in the record (nobody took attendance) must not read the
        // same as the student having been there and marked absent.
        var tally = AttendanceRate.Resolve([Marked(AttendanceState.Present), HeldButUnmarked]);

        tally.Recorded.Should().Be(1);
        tally.Present.Should().Be(1);
        tally.Absent.Should().Be(0);
        tally.Rate.Should().Be(1m);
    }

    [Fact]
    public void Resolve_PresentAndLate_BothCountAsAttendedInTheRate()
    {
        var tally = AttendanceRate.Resolve(
        [
            Marked(AttendanceState.Present),
            Marked(AttendanceState.Late),
            Marked(AttendanceState.Absent),
            Marked(AttendanceState.Excused),
        ]);

        tally.Recorded.Should().Be(4);
        tally.Present.Should().Be(1);
        tally.Late.Should().Be(1);
        tally.Absent.Should().Be(1);
        tally.Excused.Should().Be(1);

        // Present + Late = 2, over 4 recorded.
        tally.Rate.Should().Be(0.5m);
    }

    [Fact]
    public void Resolve_ExcusedAbsence_CountsAgainstTheRateLikeAnyOtherAbsence()
    {
        // An excuse changes why the student was not there, not whether they
        // were — the rate still reads it as a miss. It is kept in its own
        // column precisely so a reader can tell the two kinds of miss apart
        // without the single number hiding which was which.
        var tally = AttendanceRate.Resolve([Marked(AttendanceState.Excused)]);

        tally.Recorded.Should().Be(1);
        tally.Rate.Should().Be(0m);
    }

    [Fact]
    public void Resolve_MixOfHeldCancelledAndUnmarkedSessions_OnlyCountsTheMarkedOnes()
    {
        var tally = AttendanceRate.Resolve(
        [
            Marked(AttendanceState.Present),
            Marked(AttendanceState.Absent),
            NotHeld,
            HeldButUnmarked,
        ]);

        tally.Recorded.Should().Be(2);
        tally.Rate.Should().Be(0.5m);
    }
}

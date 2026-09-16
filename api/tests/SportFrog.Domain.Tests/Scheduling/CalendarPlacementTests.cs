using AwesomeAssertions;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Domain.Tests.Scheduling;

/// <summary>
/// Places one jornada's fixtures onto the pitches an organization has said a
/// competition may use, given what is already booked. One call is always one
/// jornada — every fixture in <c>pending</c> shares the same duration — but
/// <c>taken</c> can carry bookings from other jornadas or other competitions,
/// on a different duration each. That mismatch is exactly what most of these
/// tests are about: a single fixed grid only works when everything shares one
/// length.
/// </summary>
public sealed class CalendarPlacementTests
{
    private static readonly Guid CourtA = Guid.NewGuid();
    private static readonly Guid CourtB = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 3, 7); // a Saturday
    private static readonly TimeSpan Offset = TimeSpan.Zero;
    private static readonly TimeOnly Opens = new(8, 0);

    private static readonly Guid[] OneCourt = [CourtA];
    private static readonly Guid[] TwoCourts = [CourtA, CourtB];

    private static PendingFixture Fixture() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(Day.ToDateTime(new TimeOnly(hour, minute)), Offset);

    // ---- Spacing within one jornada ---------------------------------------

    [Fact]
    public void Place_TwoFixturesOneCourt_SpacesTheSecondByDurationPlusBuffer()
    {
        var placements = CalendarPlacement.Place(
            [Fixture(), Fixture()],
            fixtureDurationMinutes: 60,
            bufferMinutes: 15,
            OneCourt,
            taken: [],
            engaged: [],
            Day,
            Offset,
            Opens);

        placements.Should().HaveCount(2);
        placements[0].At.Should().Be(At(8, 0));
        placements[0].EndsAt.Should().Be(At(9, 15)); // 60 min game + 15 min buffer
        placements[1].At.Should().Be(At(9, 15));
    }

    [Fact]
    public void Place_ZeroBuffer_PacksFixturesBackToBackWithNoGap()
    {
        var placements = CalendarPlacement.Place(
            [Fixture(), Fixture()],
            fixtureDurationMinutes: 40,
            bufferMinutes: 0,
            OneCourt,
            taken: [],
            engaged: [],
            Day,
            Offset,
            Opens);

        placements[0].EndsAt.Should().Be(At(8, 40));
        placements[1].At.Should().Be(At(8, 40));
    }

    // ---- Using every free pitch, not just the first ------------------------

    [Fact]
    public void Place_TwoFreeCourts_UsesBothAtOnceRatherThanFillingOneFirst()
    {
        // Two fixtures that could both start at 08:00, one court each. A
        // search that exhausts the first court before trying the second
        // would put both on Court A back to back instead.
        var placements = CalendarPlacement.Place(
            [Fixture(), Fixture()],
            fixtureDurationMinutes: 60,
            bufferMinutes: 0,
            TwoCourts,
            taken: [],
            engaged: [],
            Day,
            Offset,
            Opens);

        placements.Should().HaveCount(2);
        placements.Should().OnlyContain(placement => placement.At == At(8, 0));
        placements.Select(placement => placement.VenueSpaceId).Should().BeEquivalentTo([CourtA, CourtB]);
    }

    // ---- Not colliding with a booking of a different duration --------------

    [Fact]
    public void Place_NewJornadaShorterThanAnExistingBooking_NeverOverlapsIt()
    {
        // A prior booking that runs 10:15-11:15 (60 minutes, starting off
        // any 40-minute grid this jornada would step through). Four
        // back-to-back 40-minute fixtures fill 08:00-08:40, 08:40-09:20,
        // 09:20-10:00, and the fourth would land at 10:00-10:40 on a naive
        // fixed-step search — which genuinely overlaps the 10:15 booking,
        // even though nothing is booked at exactly 10:00 or 10:40.
        var existing = new Booking(CourtA, At(10, 15), At(11, 15));

        var placements = CalendarPlacement.Place(
            [Fixture(), Fixture(), Fixture(), Fixture()],
            fixtureDurationMinutes: 40,
            bufferMinutes: 0,
            OneCourt,
            taken: [existing],
            engaged: [],
            Day,
            Offset,
            Opens);

        placements.Should().HaveCount(4);
        placements.Should().OnlyContain(placement =>
            placement.At >= existing.EndsAt || placement.EndsAt <= existing.At);
        placements[3].At.Should().Be(At(11, 15)); // jumped straight to when the pitch frees up
    }

    // ---- Start time anchors every day this run reaches ----------------------

    [Fact]
    public void Place_StartTime_AnchorsEveryDayThisRunRollsInto()
    {
        // The second fixture shares its home team with the first, so once
        // the first is placed on Day, that team is busy for the rest of Day
        // and the second has to spill to Day+1 — with no per-pitch opening
        // hour left to fall back on, it has to reuse the same 12:00 asked
        // for on day one rather than reverting to some other default.
        var first = Fixture();
        var second = new PendingFixture(Guid.NewGuid(), first.HomeTeamId, Guid.NewGuid());

        var placements = CalendarPlacement.Place(
            [first, second],
            fixtureDurationMinutes: 60,
            bufferMinutes: 0,
            OneCourt,
            taken: [],
            engaged: [],
            Day,
            Offset,
            startTime: new TimeOnly(12, 0));

        placements.Should().HaveCount(2);
        placements[0].At.Should().Be(new DateTimeOffset(Day.ToDateTime(new TimeOnly(12, 0)), Offset));
        placements[1].At.Date.Should().Be(Day.AddDays(1).ToDateTime(TimeOnly.MinValue).Date);
        placements[1].At.TimeOfDay.Should().Be(new TimeOnly(12, 0).ToTimeSpan());
    }

    // ---- No cutoff on how late a match may run ------------------------------

    [Fact]
    public void Place_ManyBackToBackFixtures_KeepsGoingWithNoClosingTimeCutoff()
    {
        // Ten 90-minute fixtures back to back from 21:00 run well past
        // midnight — nothing bounds how late a match may start, or how far
        // a run of them may stretch, even across a calendar day boundary
        // (see the class remarks: reserving a pitch is a real-world
        // arrangement the organizer settles, not this algorithm's
        // business). Each lands exactly where the previous one freed the
        // pitch, with nothing ever forcing a jump to a later day.
        var fixtures = Enumerable.Range(0, 10).Select(_ => Fixture()).ToList();

        var placements = CalendarPlacement.Place(
            fixtures,
            fixtureDurationMinutes: 90,
            bufferMinutes: 0,
            OneCourt,
            taken: [],
            engaged: [],
            Day,
            Offset,
            startTime: new TimeOnly(21, 0));

        placements.Should().HaveCount(10);
        placements[0].At.Should().Be(At(21, 0));
        for (var i = 1; i < placements.Count; i++)
        {
            placements[i].At.Should().Be(placements[i - 1].EndsAt);
        }
        placements[^1].EndsAt.Should().Be(At(21, 0) + TimeSpan.FromMinutes(90 * 10));
    }

    // ---- Existing behaviour preserved --------------------------------------

    [Fact]
    public void Place_ATeamAlreadyEngagedThatDay_SkipsToTheNextDay()
    {
        var fixture = Fixture();

        var placements = CalendarPlacement.Place(
            [fixture],
            fixtureDurationMinutes: 60,
            bufferMinutes: 0,
            OneCourt,
            taken: [],
            engaged: [(fixture.HomeTeamId, Day)],
            Day,
            Offset,
            Opens);

        placements.Should().ContainSingle();
        placements[0].At.Date.Should().Be(Day.AddDays(1).ToDateTime(TimeOnly.MinValue).Date);
    }

    [Fact]
    public void Place_NoSpacesConfigured_PlacesNothing()
    {
        var placements = CalendarPlacement.Place(
            [Fixture()], 60, 15, spaceIds: [], [], [], Day, Offset, Opens);

        placements.Should().BeEmpty();
    }

    [Fact]
    public void Place_ZeroDuration_PlacesNothingRatherThanLoopingForever()
    {
        var placements = CalendarPlacement.Place(
            [Fixture()], fixtureDurationMinutes: 0, bufferMinutes: 15, OneCourt, [], [], Day, Offset, Opens);

        placements.Should().BeEmpty();
    }
}

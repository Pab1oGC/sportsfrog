using AwesomeAssertions;
using SportFrog.Api.Features.Performances;

namespace SportFrog.Api.Tests.Features.Performances;

/// <summary>
/// The message itself, kept pure and tested without a database — same
/// reasoning as <c>Matches.MatchRescheduleNotificationJobTests</c>.
/// </summary>
public sealed class PerformanceRescheduleNotificationJobTests
{
    [Fact]
    public void Body_NoScheduleOrVenue_SaysSoInsteadOfBlank()
    {
        var notice = new PerformanceRescheduleNotificationJob.Notice(
            "Equipo A", ScheduledOn: null, OrderNumber: null,
            VenueName: null, SpaceName: null, ClubEmail: null, ClubName: "Club A");

        var body = PerformanceRescheduleNotificationJob.Body(notice);

        body.Should().Contain("un día a confirmar");
        body.Should().Contain("una sede a confirmar");
        body.Should().Contain("Equipo A");
        body.Should().NotContain("turno");
    }

    [Fact]
    public void Body_WithDayVenueAndTurn_NamesAllThree()
    {
        var notice = new PerformanceRescheduleNotificationJob.Notice(
            "Equipo A", ScheduledOn: new DateOnly(2026, 5, 1), OrderNumber: 3,
            VenueName: "Sede Central", SpaceName: "Tapete 1", ClubEmail: "club@club.com", ClubName: "Club A");

        var body = PerformanceRescheduleNotificationJob.Body(notice);

        body.Should().Contain("01/05/2026");
        body.Should().Contain("turno 3");
        body.Should().Contain("Sede Central — Tapete 1");
        body.Should().NotContain("a confirmar");
    }

    [Fact]
    public void Body_DayWithNoTurnYet_OmitsTheTurnClauseEntirely()
    {
        var notice = new PerformanceRescheduleNotificationJob.Notice(
            "Equipo A", ScheduledOn: new DateOnly(2026, 5, 1), OrderNumber: null,
            VenueName: "Sede Central", SpaceName: "Tapete 1", ClubEmail: null, ClubName: "Club A");

        var body = PerformanceRescheduleNotificationJob.Body(notice);

        body.Should().Contain("01/05/2026");
        body.Should().NotContain("turno");
    }

    [Fact]
    public void FormatDay_UsesAFixedFormat_NotTheRunningCulture()
    {
        var formatted = PerformanceRescheduleNotificationJob.FormatDay(new DateOnly(2026, 12, 31));

        formatted.Should().Be("31/12/2026");
    }
}

using AwesomeAssertions;
using SportFrog.Api.Features.Matches;

namespace SportFrog.Api.Tests.Features.Matches;

/// <summary>
/// The one rule that actually matters — who gets told, and how many times —
/// kept pure and tested without a database. See
/// <see cref="MatchRescheduleNotificationJob.BuildRecipients"/>'s remarks.
/// </summary>
public sealed class MatchRescheduleNotificationJobTests
{
    [Fact]
    public void BuildRecipients_BothClubsHaveAnAddress_BothAreTold()
    {
        var recipients = MatchRescheduleNotificationJob.BuildRecipients(
            "local@club.com", "Local", "visitante@club.com", "Visitante");

        recipients.Should().HaveCount(2);
        recipients.Should().Contain(r => r.Email == "local@club.com" && r.ClubName == "Local");
        recipients.Should().Contain(r => r.Email == "visitante@club.com" && r.ClubName == "Visitante");
    }

    [Fact]
    public void BuildRecipients_NeitherClubHasAnAddress_NobodyIsTold()
    {
        var recipients = MatchRescheduleNotificationJob.BuildRecipients(null, "Local", null, "Visitante");

        recipients.Should().BeEmpty();
    }

    [Fact]
    public void BuildRecipients_OnlyOneClubHasAnAddress_OnlyThatOneIsTold()
    {
        var recipients = MatchRescheduleNotificationJob.BuildRecipients(
            "local@club.com", "Local", null, "Visitante");

        recipients.Should().ContainSingle(r => r.Email == "local@club.com");
    }

    [Fact]
    public void BuildRecipients_SameClubBothSides_IsToldOnce()
    {
        // Un club contra su propio reserva: la misma direccion no tiene por
        // que recibir el mismo aviso dos veces.
        var recipients = MatchRescheduleNotificationJob.BuildRecipients(
            "club@club.com", "Club", "club@club.com", "Club Reserva");

        recipients.Should().ContainSingle();
    }

    [Fact]
    public void BuildRecipients_SameAddressDifferentCasing_IsToldOnce()
    {
        // citext en la columna, misma idea aca: dos direcciones que solo
        // difieren en mayusculas son la misma direccion.
        var recipients = MatchRescheduleNotificationJob.BuildRecipients(
            "Club@Club.com", "Local", "club@club.com", "Visitante");

        recipients.Should().ContainSingle();
    }

    [Fact]
    public void FormatInstant_UsesAFixedFormat_NotTheRunningCulture()
    {
        var at = new DateTimeOffset(2026, 5, 1, 15, 30, 0, TimeSpan.Zero).ToLocalTime();

        var formatted = MatchRescheduleNotificationJob.FormatInstant(at);

        formatted.Should().Be(at.ToString("dd/MM/yyyy hh:mm tt", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Body_NoScheduledAtOrVenue_SaysSoInsteadOfBlank()
    {
        var notice = new MatchRescheduleNotificationJob.Notice(
            "Local", "Visitante", ScheduledAt: null, VenueName: null, SpaceName: null, Recipients: []);

        var body = MatchRescheduleNotificationJob.Body(notice);

        body.Should().Contain("una fecha a confirmar");
        body.Should().Contain("una sede a confirmar");
        body.Should().Contain("Local vs Visitante");
    }

    [Fact]
    public void Body_WithScheduleAndVenue_NamesBoth()
    {
        var notice = new MatchRescheduleNotificationJob.Notice(
            "Local", "Visitante",
            ScheduledAt: new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
            VenueName: "Sede Central", SpaceName: "Cancha 1", Recipients: []);

        var body = MatchRescheduleNotificationJob.Body(notice);

        body.Should().Contain("Sede Central — Cancha 1");
        body.Should().NotContain("a confirmar");
    }
}

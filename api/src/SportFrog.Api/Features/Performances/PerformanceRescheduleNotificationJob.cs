using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Email;
using SportFrog.Api.Infrastructure.Jobs;

namespace SportFrog.Api.Features.Performances;

/// <summary>
/// Tells a competitor's club its turn in a classification stage's running
/// order moved: to which day, which mat, and which position in the queue.
/// </summary>
/// <remarks>
/// The running-order sibling of <see cref="Matches.MatchRescheduleNotificationJob"/>,
/// with the one difference that follows from <see cref="Performance"/> having
/// one side instead of two: there is exactly one club to write to, never a
/// pair to dedupe. Same reason for running after the response has already
/// gone out, and same reason a club with no
/// <see cref="Infrastructure.Persistence.Entities.Club.ContactEmail"/> on
/// file is an ordinary outcome and not a failure.
///
/// Deliberately does not attempt to say a time of day — see
/// <see cref="Performance.OrderNumber"/>'s remarks on why a running order
/// carries no clock time worth reporting. What moved is a day, a mat and a
/// position in the queue, and that is exactly what this says moved.
/// </remarks>
public sealed class PerformanceRescheduleNotificationJob(
    OrganizationJobScope scopes, IEmailSender email, ILogger<PerformanceRescheduleNotificationJob> logger)
{
    internal sealed record Notice(
        string TeamName,
        DateOnly? ScheduledOn,
        short? OrderNumber,
        string? VenueName,
        string? SpaceName,
        string? ClubEmail,
        string ClubName);

    public async Task RunAsync(
        Guid organizationId, Guid userId, Guid performanceId, CancellationToken cancellationToken)
    {
        var notice = await ReadAsync(organizationId, userId, performanceId, cancellationToken);

        if (notice is null)
        {
            // Gone, or moved again since this was queued. Nothing to tell
            // anybody about a slot in whatever state it is now.
            logger.LogInformation(
                "Performance {Performance} of organization {Organization} was not found; no notice sent.",
                performanceId, organizationId);

            return;
        }

        if (string.IsNullOrWhiteSpace(notice.ClubEmail))
        {
            // No contact email on file for this club. Not an error — most
            // installations have none configured yet — just nothing to do.
            return;
        }

        try
        {
            await email.SendAsync(notice.ClubEmail, $"Reprogramación: {notice.TeamName}", Body(notice), cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogWarning(
                failure,
                "Could not notify {ClubName} <{Email}> about performance {Performance}.",
                notice.ClubName, notice.ClubEmail, performanceId);
        }
    }

    private Task<Notice?> ReadAsync(
        Guid organizationId, Guid userId, Guid performanceId, CancellationToken cancellationToken) =>
        scopes.RunAsync(organizationId, userId, async (_, database) =>
            await database.Performances
                .AsNoTracking()
                .Where(candidate => candidate.Id == performanceId)
                .Select(candidate => new Notice(
                    candidate.Team!.Name,
                    candidate.ScheduledOn,
                    candidate.OrderNumber,
                    candidate.VenueSpace != null ? candidate.VenueSpace.Venue!.Name : null,
                    candidate.VenueSpace != null ? candidate.VenueSpace.Name : null,
                    candidate.Team.Club!.ContactEmail,
                    candidate.Team.Club.Name))
                .SingleOrDefaultAsync(cancellationToken),
        cancellationToken);

    /// <summary>A day, not an instant — see the class remarks on why.</summary>
    internal static string FormatDay(DateOnly day) =>
        day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    internal static string Body(Notice notice)
    {
        var when = notice.ScheduledOn is { } day ? FormatDay(day) : "un día a confirmar";
        var turn = notice.OrderNumber is { } order ? $", turno {order}" : "";
        var where = notice.VenueName is not null
            ? $"{notice.VenueName} — {notice.SpaceName}"
            : "una sede a confirmar";

        return
            $"Se reprogramó la actuación de {notice.TeamName}.\n\n" +
            $"Nuevo día: {when}{turn}\n" +
            $"Nueva sede: {where}\n\n" +
            "Este es un aviso automático de SportFrog.";
    }
}

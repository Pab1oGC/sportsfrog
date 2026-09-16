using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Email;
using SportFrog.Api.Infrastructure.Jobs;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Tells both clubs of a fixture that it moved: to when, and to where.
/// </summary>
/// <remarks>
/// Runs after the response that moved the fixture has already gone out —
/// same reason as <c>ImportAthletePhotos.Queue</c>: the request's own
/// transaction has to actually commit before a job reading the new schedule
/// can see it, and a request that rolls back afterwards must never have
/// already told anybody about a change that never happened.
///
/// A club with no <see cref="SportFrog.Api.Infrastructure.Persistence.Entities.Club.ContactEmail"/> on file is an
/// ordinary outcome, not a failure: the fixture still moved, there is just
/// nobody to write to. One address failing to send does not stop the other
/// from being tried, and does not fail the job — Hangfire retrying the whole
/// notification because half of it bounced would tell the club that did
/// receive it a second time for no reason.
/// </remarks>
public sealed class MatchRescheduleNotificationJob(
    OrganizationJobScope scopes, IEmailSender email, ILogger<MatchRescheduleNotificationJob> logger)
{
    internal sealed record Recipient(string Email, string ClubName);

    internal sealed record Notice(
        string HomeTeamName,
        string AwayTeamName,
        DateTimeOffset? ScheduledAt,
        string? VenueName,
        string? SpaceName,
        IReadOnlyList<Recipient> Recipients);

    public async Task RunAsync(
        Guid organizationId, Guid userId, Guid matchId, CancellationToken cancellationToken)
    {
        var notice = await ReadAsync(organizationId, userId, matchId, cancellationToken);

        if (notice is null)
        {
            // Gone, or moved again since this was queued. Nothing to tell
            // anybody about a fixture in whatever state it is now.
            logger.LogInformation(
                "Match {Match} of organization {Organization} was not found; no notice sent.",
                matchId, organizationId);

            return;
        }

        if (notice.Recipients.Count == 0)
        {
            // Both clubs have no contact email on file. Not an error — most
            // installations have none configured yet — just nothing to do.
            return;
        }

        var subject = $"Reprogramación: {notice.HomeTeamName} vs {notice.AwayTeamName}";
        var body = Body(notice);

        foreach (var recipient in notice.Recipients)
        {
            try
            {
                await email.SendAsync(recipient.Email, subject, body, cancellationToken);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                logger.LogWarning(
                    failure,
                    "Could not notify {ClubName} <{Email}> about match {Match}.",
                    recipient.ClubName, recipient.Email, matchId);
            }
        }
    }

    private Task<Notice?> ReadAsync(
        Guid organizationId, Guid userId, Guid matchId, CancellationToken cancellationToken) =>
        scopes.RunAsync(organizationId, userId, async (_, database) =>
        {
            var match = await database.Matches
                .AsNoTracking()
                .Where(candidate => candidate.Id == matchId)
                .Select(candidate => new
                {
                    HomeTeamName = candidate.HomeTeam!.Name,
                    AwayTeamName = candidate.AwayTeam!.Name,
                    candidate.ScheduledAt,
                    VenueName = candidate.VenueSpace != null ? candidate.VenueSpace.Venue!.Name : null,
                    SpaceName = candidate.VenueSpace != null ? candidate.VenueSpace.Name : null,
                    HomeClubName = candidate.HomeTeam.Club!.Name,
                    HomeClubEmail = candidate.HomeTeam.Club.ContactEmail,
                    AwayClubName = candidate.AwayTeam.Club!.Name,
                    AwayClubEmail = candidate.AwayTeam.Club.ContactEmail,
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (match is null)
            {
                return null;
            }

            var recipients = BuildRecipients(
                match.HomeClubEmail, match.HomeClubName, match.AwayClubEmail, match.AwayClubName);

            return new Notice(
                match.HomeTeamName, match.AwayTeamName, match.ScheduledAt,
                match.VenueName, match.SpaceName, recipients);
        },
        cancellationToken);

    /// <summary>
    /// Pure on purpose — kept apart from the query above so the one rule that
    /// actually matters (who gets told, and how many times) is cheap to test
    /// without a database.
    /// </summary>
    internal static List<Recipient> BuildRecipients(
        string? homeEmail, string homeClubName, string? awayEmail, string awayClubName)
    {
        var recipients = new List<Recipient>();

        if (!string.IsNullOrWhiteSpace(homeEmail))
        {
            recipients.Add(new Recipient(homeEmail, homeClubName));
        }

        // The same club fielding both sides — a club against its own reserve
        // team — is told once, not twice.
        if (!string.IsNullOrWhiteSpace(awayEmail)
            && !string.Equals(awayEmail, homeEmail, StringComparison.OrdinalIgnoreCase))
        {
            recipients.Add(new Recipient(awayEmail, awayClubName));
        }

        return recipients;
    }

    /// <summary>
    /// A fixed format, not one <c>ToString()</c> would pick per the server's
    /// own culture — same reasoning as the frontend's own <c>fechaHora</c>:
    /// an email is read on whatever device the recipient has, and a format
    /// that changed with wherever this process happens to be deployed would
    /// be a support question waiting to happen.
    /// </summary>
    internal static string FormatInstant(DateTimeOffset at) =>
        // Converted to this process's own local time. No timezone is stored
        // anywhere in this system — the frontend's own datetime picker makes
        // the same assumption converting the other way, at the moment a
        // fixture is scheduled — so this is only correct when the server
        // runs in the same zone as the competition. True for a single
        // regional deployment, the only kind this system runs as today.
        at.ToLocalTime().ToString("dd/MM/yyyy hh:mm tt", CultureInfo.InvariantCulture);

    internal static string Body(Notice notice)
    {
        var when = notice.ScheduledAt is { } at ? FormatInstant(at) : "una fecha a confirmar";
        var where = notice.VenueName is not null
            ? $"{notice.VenueName} — {notice.SpaceName}"
            : "una sede a confirmar";

        return
            $"Se reprogramó el partido {notice.HomeTeamName} vs {notice.AwayTeamName}.\n\n" +
            $"Nueva fecha y hora: {when}\n" +
            $"Nueva sede: {where}\n\n" +
            "Este es un aviso automático de SportFrog.";
    }
}

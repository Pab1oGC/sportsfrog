using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Email;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// Mints a reset token for an account and mails the link to it.
/// </summary>
/// <remarks>
/// Runs after the response to <see cref="RequestPasswordReset"/> has already
/// gone out, for two reasons that both matter.
///
/// The first is the one <see cref="Matches.MatchRescheduleNotificationJob"/>
/// has: a relay can be slow or unreachable, and nobody asking to recover
/// their password should be left waiting on an SMTP handshake — or told
/// anything by how long it took.
///
/// The second is specific to this one. If the endpoint minted the token and
/// enqueued it, the token would travel as a job argument, and Hangfire keeps
/// job arguments in the database — in plain text, past the life of the
/// token. That is the one thing the whole design of this table avoids (see
/// the migration's SQL). So the account id is what gets enqueued, which is
/// no secret at all, and the token is minted here, used once, and never
/// written anywhere but as a digest.
///
/// Carries no organization scope, unlike most jobs: <c>users</c> and
/// <c>password_reset_tokens</c> have no isolation policy, so there is no
/// context to establish — see <see cref="Infrastructure.Jobs.OrganizationJobScope"/>
/// for the jobs that do.
/// </remarks>
public sealed class SendPasswordResetJob(
    SportFrogDbContext database,
    PasswordResetTokens tokens,
    IEmailSender email,
    IOptions<PasswordResetOptions> options,
    ILogger<SendPasswordResetJob> logger)
{
    public async Task RunAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            // What RequestPasswordReset queues for an address that has no
            // account: the work is enqueued either way so that the two cases
            // cannot be told apart by how long the response took, and this is
            // where the one with nothing behind it stops. Debug, not
            // Information — at one of these per unregistered address tried,
            // it is the kind of line that buries the real ones.
            logger.LogDebug("No account was found for a password reset request; nothing to send.");

            return;
        }

        var settings = options.Value;

        if (settings.ResetBaseUrl.Length == 0)
        {
            // Checked by the endpoint before enqueueing, so reaching this is
            // a deployment that lost the setting between the two. Logged
            // rather than thrown: retrying cannot fix a missing setting, and
            // a job failing forever is noise that hides a real failure.
            logger.LogWarning(
                "PasswordReset:ResetBaseUrl is not configured; no reset mail sent to user {User}.",
                userId);

            return;
        }

        // Re-read rather than trusted from the argument: the account may have
        // been disabled or deleted between the request and this job, and the
        // global filter on users already hides a soft-deleted one.
        var account = await database.Users
            .Where(candidate => candidate.Id == userId && candidate.IsActive)
            .Select(candidate => new { candidate.Email })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            logger.LogInformation(
                "User {User} is no longer eligible for a password reset; no mail sent.", userId);

            return;
        }

        // Minted only now that there is somewhere to send it. A token issued
        // for a mail that was never going out is a live credential nobody
        // asked for.
        var token = await tokens.IssueAsync(userId, cancellationToken);
        var link = PasswordResetMail.Link(settings.ResetBaseUrl, token);

        await email.SendAsync(
            account.Email,
            PasswordResetMail.Subject,
            PasswordResetMail.Body(link, settings.TokenLifetime),
            PasswordResetMail.HtmlBody(link, settings.TokenLifetime),
            cancellationToken);
    }
}

namespace SportFrog.Api.Infrastructure.Email;

/// <summary>
/// Sends a notice to one address, in plain text and — when the caller built
/// one, see <see cref="EmailTemplate"/> — in HTML too.
/// </summary>
/// <remarks>
/// One address and one message, not a batch: every caller today is a
/// background job telling one club about one fixture, so there is nothing to
/// gain from a wider shape and a real cost to guessing at one before a second
/// caller ever asks for it.
///
/// <paramref name="body"/> always travels: it is what a text-only client
/// renders, and what <see cref="NullEmailSender"/> logs when nothing is
/// configured to send at all. <paramref name="htmlBody"/> is optional so a
/// caller with nothing worth formatting is not forced to build one.
/// </remarks>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, string? htmlBody, CancellationToken cancellationToken);
}

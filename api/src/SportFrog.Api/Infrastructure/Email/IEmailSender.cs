namespace SportFrog.Api.Infrastructure.Email;

/// <summary>
/// Sends a plain-text notice to one address.
/// </summary>
/// <remarks>
/// One address and one message, not a batch: every caller today is a
/// background job telling one club about one fixture, so there is nothing to
/// gain from a wider shape and a real cost to guessing at one before a second
/// caller ever asks for it.
/// </remarks>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

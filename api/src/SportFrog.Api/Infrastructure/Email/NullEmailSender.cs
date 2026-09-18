namespace SportFrog.Api.Infrastructure.Email;

/// <summary>
/// Stands in for <see cref="SmtpEmailSender"/> when no relay is configured.
/// </summary>
/// <remarks>
/// An installation that never fills in <see cref="SmtpOptions"/> still has to
/// run — every screen, every test, a fresh clone with an empty appsettings —
/// and a feature that emails a club when its fixture moves must not be what
/// stands between that and a working system. This logs what would have gone
/// out and returns, the same shape a real send has from every caller's point
/// of view.
/// </remarks>
internal sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, string? htmlBody, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "SMTP is not configured; not sending to {To}: {Subject}", to, subject);

        return Task.CompletedTask;
    }
}

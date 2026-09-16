using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace SportFrog.Api.Infrastructure.Email;

/// <summary>Sends mail through a configured SMTP relay.</summary>
/// <remarks>
/// Only ever constructed by <see cref="EmailSenderFactory"/>, once
/// <see cref="SmtpOptions.Host"/> is known to be set — this class does not
/// check that itself, the same division of labour <c>ObjectStore</c> and its
/// options have: one place decides whether the integration is on, everything
/// downstream of that assumes it is.
/// </remarks>
internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect,
                cancellationToken);

            if (_options.Username.Length > 0)
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            // Best effort: a relay that already dropped the connection after
            // rejecting the message does not deserve a second failure logged
            // over the disconnect.
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(true, cancellationToken);
                }
                catch (Exception failure)
                {
                    logger.LogWarning(failure, "Could not cleanly disconnect from the SMTP relay.");
                }
            }
        }
    }
}

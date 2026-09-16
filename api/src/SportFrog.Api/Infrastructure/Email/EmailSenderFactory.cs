using Microsoft.Extensions.Options;

namespace SportFrog.Api.Infrastructure.Email;

/// <summary>
/// Decides once, per resolution, whether outgoing mail is real or logged.
/// </summary>
/// <remarks>
/// The one place that reads <see cref="SmtpOptions.Host"/> to make that call
/// — see its remarks for why blank is a legitimate, unconfigured state rather
/// than a startup failure.
/// </remarks>
internal static class EmailSenderFactory
{
    public static IEmailSender Create(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<SmtpOptions>>().Value;

        return options.Host.Length > 0
            ? new SmtpEmailSender(services.GetRequiredService<IOptions<SmtpOptions>>(), services.GetRequiredService<ILogger<SmtpEmailSender>>())
            : new NullEmailSender(services.GetRequiredService<ILogger<NullEmailSender>>());
    }
}

namespace SportFrog.Api.Infrastructure.Email;

/// <summary>
/// Where to send outgoing mail through, and under what identity.
/// </summary>
/// <remarks>
/// Deliberately optional, unlike <see cref="Storage.StorageOptions"/>: object
/// storage is load-bearing from the first request the API serves, but an
/// installation can run — every screen, every endpoint — without ever
/// sending an email. <see cref="Host"/> left blank is how "not configured
/// yet" is spelled; nothing here fails startup over it, and
/// <see cref="EmailSenderFactory"/> reads exactly that to decide whether real
/// mail goes out or a line goes to the log instead.
/// </remarks>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>The relay's address. Blank means no relay is configured.</summary>
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// STARTTLS on an ordinary submission port (587), rather than TLS from the
    /// first byte (465) — what nearly every relay actually expects. Off only
    /// for a local, unencrypted test server.
    /// </summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>The address mail appears to come from.</summary>
    public string FromAddress { get; set; } = "no-reply@sportfrog.app";

    public string FromName { get; set; } = "SportFrog";
}

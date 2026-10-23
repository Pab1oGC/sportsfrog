namespace SportFrog.Api.Features.Auth;

/// <summary>
/// Where a reset link sends whoever clicks it, and how long it stays good
/// for.
/// </summary>
/// <remarks>
/// Optional and never ValidateOnStart, unlike
/// <see cref="Documents.DocumentOptions"/> — which is required because it is
/// printed on cards that cannot be reissued. This follows
/// <see cref="Infrastructure.Email.SmtpOptions"/> instead: an installation
/// can run every screen and every endpoint without ever having set up
/// password recovery, and failing startup over a feature nobody has asked
/// for would make every other one depend on it.
///
/// <see cref="ResetBaseUrl"/> left blank is how "not configured yet" is
/// spelled, and <see cref="RequestPasswordReset"/> reads exactly that to
/// decide whether a mail can be built at all. It still answers the caller
/// the same either way: whether this installation has recovery configured
/// is not something an anonymous caller gets to learn.
///
/// In practice it is never blank: the committed appsettings.json carries the
/// deployment's real public address and appsettings.Development.json
/// overrides it with the dev server's, so neither a deployment nor a fresh
/// clone has to set anything for a mailed link to be right. An environment
/// variable still wins over both, for an installation served from somewhere
/// else. The blank path stays because a wrong override must degrade to "no
/// mail" rather than to a link pointing nowhere.
///
/// The address cannot be derived from the request. The page lives in the
/// frontend, on a different origin than the API, and behind a reverse proxy
/// the host the API sees is the proxy rather than the public domain — which
/// is also why it is configuration and not something read off
/// <c>VITE_API_URL</c>: that one names the API, is baked into the browser
/// bundle at build time, and this process never sees it.
/// </remarks>
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>
    /// The page the mailed link leads to, with the token appended as a query
    /// parameter. Blank means no relay of our own is configured and no reset
    /// mail goes out.
    /// </summary>
    public string ResetBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// How long a reset token stays redeemable.
    /// </summary>
    /// <remarks>
    /// An hour, far shorter than a renewal token's thirty days
    /// (<see cref="Infrastructure.Auth.SessionIssuer.RefreshTokenLifetime"/>),
    /// because the two bound different things. A renewal token is held by a
    /// client that keeps rotating it; this one sits in an inbox, which is
    /// exactly where a stale one is read by somebody who should not have it.
    /// Long enough to walk to a computer, short enough that a forwarded
    /// message is worthless by the time it arrives.
    /// </remarks>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);
}

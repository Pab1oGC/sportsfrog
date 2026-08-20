using System.ComponentModel.DataAnnotations;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// How access tokens are signed and how long they last.
///
/// The signing key is a secret and never lives in the repository: it arrives
/// from the environment, like the connection strings. Startup fails if it is
/// missing rather than falling back to a default, because a well-known
/// signing key is the same as no signature at all.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    /// <summary>Shortest key HS256 accepts without weakening the MAC.</summary>
    public const int MinimumSigningKeyLength = 32;

    [Required(AllowEmptyStrings = false)]
    [MinLength(MinimumSigningKeyLength)]
    public string SigningKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Deliberately short: it bounds the window of misuse if a token leaks.
    /// Staying signed in past it is the renewal token's job, which — unlike
    /// this one — can be revoked.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
}

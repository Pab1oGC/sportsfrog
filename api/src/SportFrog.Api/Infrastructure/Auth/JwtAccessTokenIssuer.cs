using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Issues and validates the short-lived access token. Everything
/// authorization decides on downstream — who the user is and which roles
/// they hold — travels in here, signed (RF-02).
///
/// A short lifetime is the point: it bounds the window of misuse if a token
/// leaks. Closing a session on demand is the renewal token's job, since a
/// signed token cannot be recalled once handed out.
/// </summary>
public sealed class JwtAccessTokenIssuer
{
    /// <summary>
    /// Claim type carrying a role. The short JWT name is used verbatim
    /// rather than the long WS-* URI, and inbound mapping is switched off on
    /// the reading side so it survives a round trip unchanged.
    /// </summary>
    public const string RoleClaimType = "role";

    /// <summary>Shortest key HS256 accepts without weakening the MAC.</summary>
    private const int MinimumSigningKeyBytes = 32;

    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _lifetime;
    private readonly TimeProvider _timeProvider;

    public JwtAccessTokenIssuer(
        string signingKey,
        string issuer,
        string audience,
        TimeSpan lifetime,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        if (keyBytes.Length < MinimumSigningKeyBytes)
        {
            throw new ArgumentException(
                $"The signing key must be at least {MinimumSigningKeyBytes} bytes for HS256.",
                nameof(signingKey));
        }

        _signingKey = new SymmetricSecurityKey(keyBytes);
        _issuer = issuer;
        _audience = audience;
        _lifetime = lifetime;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Issues a token for a user and the roles they hold.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The user is absent, or the role collection is empty or holds a blank
    /// entry. A token with no roles authorizes nothing and signals a bug
    /// upstream: every membership carries exactly one role.
    /// </exception>
    public string Issue(Guid userId, IReadOnlyCollection<string> roles)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The user identifier is required.", nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(roles);

        if (roles.Count == 0)
        {
            throw new ArgumentException("At least one role is required.", nameof(roles));
        }

        if (roles.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A role cannot be blank.", nameof(roles));
        }

        var issuedAt = _timeProvider.GetUtcNow();

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),

            // Distinguishes two tokens issued for the same user in the same
            // instant, which otherwise would be indistinguishable in a log.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            .. roles.Select(role => new Claim(RoleClaimType, role)),
        ];

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: issuedAt.Add(_lifetime).UtcDateTime,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Validates signature, issuer, audience and lifetime, and requires the
    /// token to name a user.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// No token was supplied. That is a defect in the caller, not a rejected
    /// credential, and is kept distinct on purpose.
    /// </exception>
    /// <exception cref="SecurityTokenException">
    /// The token is unreadable, tampered with, expired, or carries no
    /// subject to attach to a user. Every refusal surfaces as this one
    /// family so the caller has a single thing to catch and answer with an
    /// authentication failure — never as an error that reads like a bug and
    /// turns into a server fault.
    /// </exception>
    public ClaimsPrincipal Validate(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        // Inbound mapping off: "sub" and "role" stay as they were written
        // instead of being renamed to their WS-* URIs.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,

                    // Only HS256 is accepted, which is what refuses a token
                    // whose algorithm was downgraded to "none".
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                },
                out _);
        }
        catch (SecurityTokenMalformedException exception)
        {
            // A string that isn't a JWT at all arrives from the library as an
            // ArgumentException, which would read as a programming fault. It
            // is a rejected credential like any other.
            throw new SecurityTokenException("The token is not a readable JWT.", exception);
        }

        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(subject) || !Guid.TryParse(subject, out _))
        {
            throw new SecurityTokenException("The token carries no usable subject claim.");
        }

        return principal;
    }
}

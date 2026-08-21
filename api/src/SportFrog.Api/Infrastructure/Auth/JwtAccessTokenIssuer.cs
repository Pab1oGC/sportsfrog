using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Issues and validates the short-lived access token. Everything
/// authorization decides on downstream — who the user is, and the role they
/// hold in each organization they belong to — travels in here, signed
/// (RF-02).
///
/// There is deliberately no way to issue a token with an unscoped role: a
/// role means nothing without the organization it applies to, and one that
/// travelled without it would grant more than intended rather than less.
///
/// A short lifetime is the point: it bounds the window of misuse if a token
/// leaks. Closing a session on demand is the renewal token's job, since a
/// signed token cannot be recalled once handed out.
/// </summary>
public sealed class JwtAccessTokenIssuer
{
    /// <summary>
    /// Prefix of the claim that grants a role inside one organization. The
    /// full claim type is this prefix followed by the organization
    /// identifier; its value is the role held there.
    ///
    /// The organization travels in the claim *type* rather than packed into
    /// the value so that checking a permission is one lookup, and — the point
    /// of it — so a role can never be read without the organization it
    /// applies to.
    /// </summary>
    public const string OrganizationClaimTypePrefix = "org:";

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
    /// Issues the token a sign-in hands out: the user, and every organization
    /// they belong to paired with the role they hold there.
    ///
    /// This is the method the authentication flow uses. It deliberately emits
    /// no unscoped role claim: a token carrying a bare "admin" would let a
    /// careless permission check pass in an organization the user is only a
    /// viewer of, which is exactly the confusion the per-organization model
    /// exists to prevent.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The user is absent, the collection is empty, an organization is
    /// absent, or one organization appears twice. A membership is unique per
    /// (organization, user), so a repeated organization means the caller
    /// built the list wrong and the token would grant two roles in one place.
    /// </exception>
    public string IssueForOrganizations(
        Guid userId,
        IReadOnlyCollection<OrganizationAccess> organizations)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The user identifier is required.", nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(organizations);

        if (organizations.Count == 0)
        {
            throw new ArgumentException(
                "The user must belong to at least one organization.", nameof(organizations));
        }

        if (organizations.Any(access => access.OrganizationId == Guid.Empty))
        {
            throw new ArgumentException(
                "An organization identifier is required.", nameof(organizations));
        }

        if (organizations.Select(access => access.OrganizationId).Distinct().Count() != organizations.Count)
        {
            throw new ArgumentException(
                "An organization cannot appear twice.", nameof(organizations));
        }

        return Write(userId, [
            .. organizations.Select(access => new Claim(
                OrganizationClaimTypePrefix + access.OrganizationId,
                ToClaimValue(access.Role))),
        ]);
    }

    /// <summary>
    /// Reads back the role a validated token grants inside one organization,
    /// or <c>null</c> when it grants none there.
    ///
    /// Returning null rather than throwing is deliberate: "this token does
    /// not reach that organization" is an authorization answer the caller has
    /// to turn into a refusal, not an exceptional condition.
    /// </summary>
    public static MembershipRole? FindRole(ClaimsPrincipal principal, Guid organizationId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var value = principal.FindFirst(OrganizationClaimTypePrefix + organizationId)?.Value;

        return Enum.TryParse<MembershipRole>(value, ignoreCase: true, out var role) ? role : null;
    }

    /// <summary>
    /// Lowercase, matching the labels of the database's
    /// <c>membership_role</c> enum, so the same word means the same thing in
    /// a token, in a log line and in a row.
    /// </summary>
    private static string ToClaimValue(MembershipRole role) =>
        role.ToString().ToLowerInvariant();

    /// <summary>
    /// Signs a token for a subject plus whatever claims describe what it
    /// grants. Both issuing paths go through here so signature, lifetime and
    /// unique identifier are decided in exactly one place.
    /// </summary>
    private string Write(Guid userId, IReadOnlyCollection<Claim> grants)
    {
        var issuedAt = _timeProvider.GetUtcNow();

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),

            // Distinguishes two tokens issued for the same user in the same
            // instant, which otherwise would be indistinguishable in a log.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            .. grants,
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
    /// <summary>
    /// The rules a token must satisfy to be trusted.
    /// </summary>
    /// <remarks>
    /// Exposed so the request pipeline's bearer authentication validates
    /// tokens by exactly the same rules this class does. Two definitions of
    /// "valid" would drift, and the looser one would become the real gate.
    /// </remarks>
    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _issuer,
        ValidateAudience = true,
        ValidAudience = _audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,

        // Only HS256 is accepted, which is what refuses a token whose
        // algorithm was downgraded to "none".
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
    };

    public ClaimsPrincipal Validate(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        // Inbound mapping off: "sub" and the "org:" claims stay as they were
        // written instead of being renamed to their WS-* URIs.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(token, CreateValidationParameters(), out _);
        }
        catch (ArgumentException exception)
        {
            // The handler reads a token before it verifies it, so anything
            // that stops it reading — a string that is not a JWT at all, a
            // payload edited into something that is no longer JSON — surfaces
            // as an ArgumentException, which would read as a programming
            // fault. These are rejected credentials like any other.
            //
            // Which refusal it is cannot be decided by the failure to read.
            // The signature covers the header and payload as they were
            // written, so it can be checked even when the payload no longer
            // parses; and a token whose bytes were altered is a token whose
            // signature no longer matches. Saying so is more accurate than
            // reporting it as unreadable, and it is the same answer a subtler
            // edit — one that leaves valid JSON — already gets.
            throw SignatureMatches(token)
                ? new SecurityTokenException("The token is not a readable JWT.", exception)
                : new SecurityTokenInvalidSignatureException(
                    "The token signature does not match its contents.", exception);
        }

        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(subject) || !Guid.TryParse(subject, out _))
        {
            throw new SecurityTokenException("The token carries no usable subject claim.");
        }

        return principal;
    }

    /// <summary>
    /// Whether the signature still matches the header and payload it was
    /// written over.
    /// </summary>
    /// <remarks>
    /// Computed here rather than asked of the handler, because this is only
    /// reached when the handler could not read the token far enough to check
    /// anything. The signature does not depend on the payload being readable:
    /// it is taken over the two segments as text, so it answers the question
    /// even when they no longer decode to anything sensible.
    ///
    /// Compared in fixed time. The comparison is not secret-dependent in any
    /// obvious way — the caller supplied both sides — but a signature check
    /// that returns early on the first differing byte is the shape of a
    /// mistake worth never making.
    /// </remarks>
    private bool SignatureMatches(string token)
    {
        var segments = token.Split('.');

        if (segments.Length != 3)
        {
            return false;
        }

        try
        {
            var signed = Encoding.UTF8.GetBytes($"{segments[0]}.{segments[1]}");
            var expected = HMACSHA256.HashData(_signingKey.Key, signed);
            var presented = Base64UrlEncoder.DecodeBytes(segments[2]);

            return CryptographicOperations.FixedTimeEquals(expected, presented);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            // The signature segment is not even base64url. Nothing matches
            // that.
            return false;
        }
    }
}

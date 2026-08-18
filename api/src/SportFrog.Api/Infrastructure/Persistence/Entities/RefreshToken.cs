namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A revocable renewal token. The access token is short-lived, which bounds
/// the window of misuse if it leaks; this one lives in the database so a
/// session can actually be closed.
///
/// Only the hash is stored: a database dump must not hand over usable
/// tokens. It has no org_id — a session belongs to a person, not to an
/// organization — so it carries no isolation policy.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Digest of the token. The token itself is never persisted.</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User? User { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}

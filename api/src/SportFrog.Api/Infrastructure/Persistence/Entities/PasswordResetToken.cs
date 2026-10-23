namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A single-use proof that whoever holds it reads the mail sent to an
/// account's registered address, and may therefore set that account's
/// password without knowing the current one.
///
/// Only the hash is stored, the same as <see cref="RefreshToken"/>: a
/// database dump must not hand over anything that can be presented. It has
/// no org_id — a password belongs to a person, not to an organization — so
/// it carries no isolation policy.
///
/// Unlike a renewal token, which is revoked when a session must stop
/// working, this one is <em>consumed</em>: it buys exactly one password
/// change and is spent by it.
/// </summary>
public sealed class PasswordResetToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Digest of the token. The token itself is never persisted.</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// When the token stopped being redeemable — because it was redeemed, or
    /// because a later request for the same account superseded it. Both are
    /// equally final, which is why one column records them rather than two.
    /// </summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// Whether the token may still be redeemed at <paramref name="now"/>.
    /// Counterpart to <see cref="RefreshToken.IsActive"/>: the rule lives on
    /// the entity so no caller has to remember that an unexpired token can
    /// still have been spent.
    /// </summary>
    public bool IsRedeemable(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;

    /// <summary>
    /// Spends the token at <paramref name="now"/>, if it has not been spent
    /// already. Idempotent, so superseding an account's outstanding tokens
    /// does not have to filter out the ones that were already consumed.
    /// </summary>
    public void Consume(DateTimeOffset now) => ConsumedAt ??= now;
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Custody of the tokens that let an account whose password is no longer
/// known set a new one: minting them, superseding the ones it replaces, and
/// spending the one that comes back.
/// </summary>
/// <remarks>
/// Issuing and redeeming live together rather than in two classes because
/// they are two halves of one invariant — a token is good exactly once, and
/// only until it expires or a newer one replaces it. Split apart, both halves
/// would still have to agree on it, which is the same coupling with a seam
/// through the middle of it.
///
/// Counterpart to <see cref="SessionIssuer"/> for the credential that stands
/// in for a password rather than for one that follows from it.
/// </remarks>
public sealed class PasswordResetTokens(
    SportFrogDbContext database,
    IOptions<PasswordResetOptions> options,
    TimeProvider clock)
{
    /// <summary>
    /// Issues a token for an account, superseding any it is still holding,
    /// and saves.
    /// </summary>
    /// <returns>
    /// The token itself, returned once. Only its digest is kept, so there is
    /// no second chance to read it.
    /// </returns>
    /// <remarks>
    /// Saves on its own, unlike <see cref="RedeemAsync"/>: nothing else is
    /// happening in this unit of work, and whoever asked for the mail is no
    /// longer waiting on it.
    ///
    /// One outstanding token per account, enforced here rather than by the
    /// schema: asking a second time has to supersede the first, and a unique
    /// constraint would answer that legitimate request with a database error
    /// instead. See the migration's SQL for the index that makes finding them
    /// cheap.
    /// </remarks>
    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var outstanding = await database.PasswordResetTokens
            .Where(token => token.UserId == userId && token.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var superseded in outstanding)
        {
            // A link that was mailed and never used must stop working the
            // moment a newer one is asked for: two live links to the same
            // account is one more inbox than the account's owner intended.
            superseded.Consume(now);
        }

        var (token, hash) = OpaqueSecret.Create();

        database.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = now.Add(options.Value.TokenLifetime),
        });

        await database.SaveChangesAsync(cancellationToken);

        return token;
    }

    /// <summary>
    /// Spends a presented token and reports whose account it opens.
    /// </summary>
    /// <returns>
    /// The account the token belongs to, or <c>null</c> when it is unknown,
    /// expired or already spent. The three are not told apart: each one means
    /// the same thing to the caller, and telling them apart would describe
    /// the state of a token to somebody who does not hold a valid one.
    /// </returns>
    /// <remarks>
    /// Deliberately does not save, the same as
    /// <see cref="SessionRevocation.RevokeEveryTokenAsync"/>. Spending the
    /// token, writing the new password hash and closing the account's open
    /// sessions are one change: a save between them would leave a window in
    /// which the token is gone and the password it was meant to set never
    /// got written.
    ///
    /// Two callers redeeming the same token at once are resolved by the
    /// database, not here: <c>consumed_at</c> is a concurrency token (see
    /// <c>PasswordResetTokenConfiguration</c>), so the second save matches no
    /// row and raises <see cref="DbUpdateConcurrencyException"/> rather than
    /// changing the password twice.
    /// </remarks>
    public async Task<Guid?> RedeemAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = OpaqueSecret.Hash(token);

        var issued = await database.PasswordResetTokens
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);

        var now = clock.GetUtcNow();

        if (issued is null || !issued.IsRedeemable(now))
        {
            return null;
        }

        issued.Consume(now);

        return issued.UserId;
    }
}

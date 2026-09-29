using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Ends every session an account holds.
/// </summary>
/// <remarks>
/// Shared by the two moments that call for it: a password change (a stolen
/// renewal token, a device left signed in, should stop working the instant
/// the account proves it knows the new password — see
/// <see cref="Features.Auth.ChangePassword"/>) and a renewal token presented
/// after it was already exchanged, the standard signal that a copy of it
/// exists somewhere the real client's own rotation never reached (see
/// <see cref="Features.Auth.RenewSession"/>). Neither call site knows which
/// other session, if any, is the compromised one, so both close all of them
/// rather than guessing.
/// </remarks>
public static class SessionRevocation
{
    public static async Task RevokeEveryTokenAsync(
        SportFrogDbContext database,
        Guid userId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var activeTokens = await database.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = at;
        }
    }
}

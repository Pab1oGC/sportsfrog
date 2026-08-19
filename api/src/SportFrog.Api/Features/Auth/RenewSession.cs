using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// Trades a renewal token for a fresh session, so a short-lived access token
/// does not mean signing in every fifteen minutes.
///
/// The presented token is revoked as part of the exchange and a new one comes
/// back with the session. Rotating rather than reusing is what keeps a
/// renewal token from becoming a long-lived credential: a copy taken from
/// storage stops working the next time the real client renews.
/// </summary>
public static class RenewSession
{
    public sealed record Request(string RefreshToken);

    public static IEndpointRouteBuilder MapRenewSession(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/auth/session/renewal", HandleAsync)
            // The access token this renews has usually expired already, which
            // is the point; the renewal token is the credential here.
            .AllowAnonymous()
            .WithoutOrganizationContext()
            .WithName(nameof(RenewSession))
            .WithSummary("Exchanges a renewal token for a new session.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        SessionIssuer sessionIssuer,
        TimeProvider clock,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Rejected();
        }

        var hash = RefreshTokenFactory.Hash(request.RefreshToken);
        var now = clock.GetUtcNow();

        // refresh_tokens carries no isolation policy: a session belongs to a
        // person, not to an organization, and it has to be found before any
        // organization is known.
        var stored = await database.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return Rejected();
        }

        if (stored.RevokedAt is not null)
        {
            // A token that was already exchanged is being presented again.
            // Either a copy outlived the real client, or two of them renewed
            // at once. Worth noticing, and refused either way.
            loggerFactory
                .CreateLogger(typeof(RenewSession))
                .LogWarning(
                    "A renewal token already revoked at {RevokedAt} was presented for user {UserId}.",
                    stored.RevokedAt,
                    stored.UserId);

            return Rejected();
        }

        if (!stored.IsActive(now) || stored.User is not { IsActive: true, DeletedAt: null })
        {
            return Rejected();
        }

        // Revoked before the replacement is issued, so a failure between the
        // two leaves the session closed rather than duplicated.
        stored.RevokedAt = now;
        await database.SaveChangesAsync(cancellationToken);

        var session = await sessionIssuer.IssueAsync(stored.UserId, cancellationToken);

        return session is null
            // Every organization was lost while the session was open. The
            // token stays revoked: there is nothing left to renew into.
            ? Rejected()
            : Results.Ok(SessionResponse.From(session));
    }

    /// <summary>
    /// One answer for every way renewing can fail: unknown, expired, already
    /// exchanged, or belonging to an account that can no longer act.
    /// </summary>
    private static IResult Rejected() =>
        Results.Problem(
            detail: "The renewal token is not valid.",
            statusCode: StatusCodes.Status401Unauthorized);
}

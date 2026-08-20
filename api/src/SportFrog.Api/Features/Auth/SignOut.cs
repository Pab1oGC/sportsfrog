using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// Closes a session by revoking its renewal token.
///
/// The access token already handed out cannot be recalled — that is what
/// being signed means — so it stays usable until it expires, minutes later.
/// What ends here is the ability to obtain another one, which is what makes
/// closing a session effective rather than cosmetic.
/// </summary>
public static class SignOut
{
    public sealed record Request(string RefreshToken);

    public static IEndpointRouteBuilder MapSignOut(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/auth/session/revocation", HandleAsync)
            // Signing out must work even when the access token has already
            // expired: being unable to end a session is worse than ending one
            // that was already over.
            .AllowAnonymous()
            .WithoutOrganizationContext()
            // Every refusal here is the same refusal. Answering 400 to a
            // malformed token would mean that a 401 confirms the token at
            // least had the right shape, which is a way of probing for one.
            .WithoutContractValidation()
            .WithName(nameof(SignOut))
            .WithSummary("Revokes a renewal token, closing the session.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = RefreshTokenFactory.Hash(request.RefreshToken);

            var stored = await database.RefreshTokens.SingleOrDefaultAsync(
                token => token.TokenHash == hash && token.RevokedAt == null,
                cancellationToken);

            if (stored is not null)
            {
                stored.RevokedAt = clock.GetUtcNow();
                await database.SaveChangesAsync(cancellationToken);
            }
        }

        // The same answer whether a session was closed, was already closed,
        // or never existed. Signing out is not a place to learn which tokens
        // are real, and a client that already discarded its token has nothing
        // useful to do with a failure.
        return Results.NoContent();
    }
}

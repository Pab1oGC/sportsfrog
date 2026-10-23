using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.RateLimiting;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// Sets a new password against a token mailed by
/// <see cref="RequestPasswordReset"/> (RF-02).
///
/// The token is what stands in for the current password that
/// <see cref="ChangePassword"/> would have asked for. It is good once, for
/// one account, and only until it expires.
/// </summary>
public static class ResetPassword
{
    public sealed record Request(string Token, string NewPassword);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Token)
                .NotEmpty()
                .WithMessage("El enlace de recuperación no es válido.");

            // Shared with ChangePassword: one statement of one policy.
            RuleFor(request => request.NewPassword).MustSatisfyPasswordPolicy();
        }
    }

    public static IEndpointRouteBuilder MapResetPassword(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/auth/password/reset", HandleAsync)
            // Anonymous by necessity: the whole point is a caller who cannot
            // sign in. The token is the credential, and it names no
            // organization — this acts on an account, like ChangePassword.
            .AllowAnonymous()
            .WithoutOrganizationContext()
            // Validated, unlike RequestPasswordReset. The contract here is
            // about a token nobody can guess and a password the caller just
            // chose, so saying plainly which of the two was wrong gives away
            // nothing about who holds an account.
            .RequireRateLimiting(RateLimitPolicies.Authentication)
            .WithName(nameof(ResetPassword))
            .WithSummary("Sets a new password using a mailed reset token.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        PasswordResetTokens resetTokens,
        BCryptPasswordHasher passwordHasher,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = await resetTokens.RedeemAsync(request.Token, cancellationToken);

        if (userId is not { } account)
        {
            return Rejected();
        }

        // Disabled or removed since the link was mailed. Refused like any
        // other unusable link: a suspended account is not one to hand a way
        // back in to, and SignIn would refuse it moments later anyway.
        var user = await database.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == account && candidate.IsActive, cancellationToken);

        if (user is null)
        {
            return Rejected();
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);

        // Exactly the reasoning ChangePassword gives: whatever was riding on
        // the old password — a device left signed in, a renewal token that
        // leaked — stops working now. It matters more here than there, since
        // the person doing this may well be acting on a suspicion that
        // somebody else has their account.
        await SessionRevocation.RevokeEveryTokenAsync(
            database, account, clock.GetUtcNow(), cancellationToken);

        try
        {
            // One save for all three changes — the token spent, the password
            // written, the sessions closed — so none of them can hold without
            // the others.
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request redeemed the same token first: consumed_at is a
            // concurrency token, so this save matched no row. Reported as an
            // invalid link, which by now is exactly what it is.
            return Rejected();
        }

        return Results.NoContent();
    }

    /// <summary>
    /// One answer for every way a link can fail to work: unknown, expired,
    /// already used, or belonging to an account that no longer exists.
    /// Telling them apart would describe the state of a token to somebody
    /// who, by definition, does not hold a valid one.
    /// </summary>
    private static IResult Rejected() =>
        Results.Problem(
            detail: "El enlace de recuperación no es válido o ya venció. Pedí uno nuevo.",
            statusCode: StatusCodes.Status401Unauthorized);
}

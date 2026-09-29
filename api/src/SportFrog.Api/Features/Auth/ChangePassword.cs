using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.RateLimiting;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Domain.ValueObjects;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// Changes the signed-in account's own password (RF-02).
///
/// Acts on the account the token names, not on an organization — the same
/// reason <see cref="Organizations.RegisterOrganization"/> runs with no
/// organization context: there is nothing here to act "inside", only an
/// identity to act on. Anyone signed in may change their own password
/// regardless of what role they hold anywhere; this is not
/// <see cref="Organizations.AddMember"/>; nobody else's password is
/// reachable through it.
/// </summary>
public static class ChangePassword
{
    public sealed record Request(string CurrentPassword, string NewPassword);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.CurrentPassword)
                .NotEmpty()
                .WithMessage("La contraseña actual es obligatoria.");

            RuleFor(request => request.NewPassword)
                .Must(password => Password.TryParse(password, out _))
                .WithMessage(
                    $"La contraseña debe tener al menos {Password.MinimumLength} caracteres e " +
                    "incluir una mayúscula, una minúscula, un número y un carácter especial.");

            // Cheap to check before ever touching the database, and the one
            // rule that is specific to changing a password rather than
            // setting one for the first time (AddMember, RegisterOrganization
            // have nothing to compare a new password against).
            RuleFor(request => request.NewPassword)
                .Must((request, newPassword) =>
                    !string.Equals(request.CurrentPassword, newPassword, StringComparison.Ordinal))
                .WithMessage("La nueva contraseña tiene que ser distinta de la actual.")
                .When(request => !string.IsNullOrEmpty(request.CurrentPassword));
        }
    }

    public static IEndpointRouteBuilder MapChangePassword(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/auth/password", HandleAsync)
            // Same reasoning as RegisterOrganization: authenticated, but
            // deliberately without an organization context — this changes
            // the account itself, not anything scoped to one.
            .WithoutOrganizationContext()
            .RequireRateLimiting(RateLimitPolicies.Authentication)
            .WithName(nameof(ChangePassword))
            .WithSummary("Changes the signed-in account's password.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        HttpContext httpContext,
        SportFrogDbContext database,
        BCryptPasswordHasher passwordHasher,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // Same 401 RegisterOrganization gives for the same reason: this
        // endpoint never establishes an organization context, so nothing
        // upstream has already refused an anonymous caller for it.
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Problem(
                detail: "Se requiere autenticación.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var userId = JwtAccessTokenIssuer.CurrentUserId(httpContext.User);

        // users carries no isolation policy — a person is not scoped to an
        // organization — so this needs no context beyond the id the token
        // already names.
        var user = await database.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == userId, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            // Same refusal either way: whether the account was somehow gone
            // by the time this ran, or the current password was simply
            // wrong, is not something worth telling apart.
            return Results.Problem(
                detail: "La contraseña actual es incorrecta.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);

        // A password change is exactly the moment a session riding on the
        // old one — a stolen renewal token, a device left signed in —
        // should stop working. The caller that just proved it knows the new
        // password signs in again with it, the same way SignOut already
        // expects a client to. Shared with RenewSession's own reuse
        // detection: see SessionRevocation.
        await SessionRevocation.RevokeEveryTokenAsync(database, userId, clock.GetUtcNow(), cancellationToken);

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

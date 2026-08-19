using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// Exchanges an email address and password for a session (RF-02).
/// </summary>
public static class SignIn
{
    public sealed record Request(string Email, string Password);

    /// <summary>
    /// A hash of nothing in particular, verified against when no account
    /// matches, so that a wrong address and a wrong password cost the same
    /// time. Without it the endpoint answers faster for addresses that do not
    /// exist, which is a way of asking whether one does.
    /// </summary>
    private const string AbsentAccountHash =
        "$2a$12$rQnJ1S4qF0hVXzKZKq0MHu5kM7Y0DkQZq6oXqE5kZ8oQxU5vT5eaC";

    public static IEndpointRouteBuilder MapSignIn(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/auth/session", HandleAsync)
            // Signing in is what produces the credentials; it cannot require
            // them, nor an organization it has not yet reported.
            .AllowAnonymous()
            .WithoutOrganizationContext()
            .WithName(nameof(SignIn))
            .WithSummary("Signs in and returns an access and a renewal token.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        BCryptPasswordHasher passwordHasher,
        SessionIssuer sessionIssuer,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim() ?? string.Empty;

        // users carries no isolation policy, and the global filter already
        // hides a soft-deleted account.
        var user = string.IsNullOrEmpty(email)
            ? null
            : await database.Users.SingleOrDefaultAsync(
                candidate => candidate.Email == email, cancellationToken);

        if (!PasswordMatches(passwordHasher, user, request.Password))
        {
            return Rejected();
        }

        var session = await sessionIssuer.IssueAsync(user!.Id, cancellationToken);

        if (session is null)
        {
            // An account that belongs nowhere can act nowhere. Answered like
            // any other failure: which accounts exist, and which of them are
            // stranded, is not something an anonymous caller gets to learn.
            return Rejected();
        }

        user.LastLoginAt = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(SessionResponse.From(session));
    }

    /// <summary>
    /// Whether the presented password belongs to a usable account.
    /// </summary>
    /// <remarks>
    /// The comparison runs even when no account matched, against a fixed
    /// hash, so the answer takes the same work either way.
    /// </remarks>
    private static bool PasswordMatches(
        BCryptPasswordHasher passwordHasher,
        User? user,
        string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var matches = passwordHasher.Verify(password, user?.PasswordHash ?? AbsentAccountHash);

        // Checked after the comparison, not instead of it, for the same
        // reason: a disabled account must not answer faster than a wrong
        // password.
        return matches && user is { IsActive: true };
    }

    /// <summary>
    /// One answer for every way signing in can fail.
    /// </summary>
    /// <remarks>
    /// A wrong password, an address with no account, a disabled account and
    /// one that belongs to no organization are all reported identically. Any
    /// difference between them would turn this endpoint into a way to ask
    /// which addresses are registered.
    /// </remarks>
    private static IResult Rejected() =>
        Results.Problem(
            detail: "The email address or password is incorrect.",
            statusCode: StatusCodes.Status401Unauthorized);
}

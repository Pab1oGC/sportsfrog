using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// Exchanges an email address and password for a session (RF-02).
///
/// Two tokens come back. The access one is short-lived and carries the role
/// held in each organization the account belongs to; the renewal one lives in
/// the database, which is what makes closing a session possible at all — a
/// signed token cannot be recalled once handed out.
/// </summary>
public static class SignIn
{
    public sealed record Request(string Email, string Password);

    /// <param name="AccessToken">Short-lived. Sent as a bearer token.</param>
    /// <param name="RefreshToken">Revocable. Returned once and never again.</param>
    /// <param name="ExpiresIn">Seconds the access token remains valid.</param>
    /// <param name="Organizations">Where the account may act, and as what.</param>
    public sealed record Response(
        string AccessToken,
        string RefreshToken,
        int ExpiresIn,
        IReadOnlyCollection<OrganizationSummary> Organizations);

    /// <param name="Id">Value for the organization header on later requests.</param>
    public sealed record OrganizationSummary(Guid Id, string Name, string Slug, string Role);

    /// <summary>
    /// How long a renewal token stays usable without being exercised.
    /// </summary>
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

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
        JwtAccessTokenIssuer tokenIssuer,
        IOptions<JwtOptions> jwtOptions,
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

        var memberships = await ReadMembershipsAsync(database, user!.Id, cancellationToken);

        if (memberships.Count == 0)
        {
            // An account that belongs nowhere can act nowhere. Answered like
            // any other failure: which accounts exist, and which of them are
            // stranded, is not something an anonymous caller gets to learn.
            return Rejected();
        }

        var now = clock.GetUtcNow();

        var accessToken = tokenIssuer.IssueForOrganizations(
            user.Id,
            [.. memberships.Select(m => new OrganizationAccess(m.OrgId, m.Role))]);

        var refreshToken = await StartSessionAsync(database, user, now, cancellationToken);

        return Results.Ok(new Response(
            accessToken,
            refreshToken,
            (int)jwtOptions.Value.AccessTokenLifetime.TotalSeconds,
            [.. memberships.Select(m => new OrganizationSummary(
                m.OrgId,
                m.Organization!.Name,
                m.Organization.Slug,
                m.Role.ToString().ToLowerInvariant()))]));
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
    /// The organizations the account belongs to, and the role held in each.
    /// </summary>
    /// <remarks>
    /// This read is what sign-in exists to resolve, and it precedes any
    /// organization context — there is nothing yet to establish one to. It
    /// works because the person is established instead: the own_memberships
    /// policy lets an account read the rows that name it.
    /// </remarks>
    private static async Task<List<OrganizationMembership>> ReadMembershipsAsync(
        SportFrogDbContext database,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_user', {0}, true)",
            [userId.ToString()],
            cancellationToken);

        var memberships = await database.Memberships
            .Include(membership => membership.Organization)
            .Where(membership => membership.UserId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // An organization that was suspended or removed grants nothing, even
        // though the membership row survives it.
        return [.. memberships.Where(m => m.Organization is { IsActive: true, DeletedAt: null })];
    }

    /// <summary>
    /// Records the session and stamps the sign-in, returning the renewal
    /// token exactly once.
    /// </summary>
    private static async Task<string> StartSessionAsync(
        SportFrogDbContext database,
        User user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var (token, hash) = RefreshTokenFactory.Create();

        database.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,

            // Only the digest is kept: a database dump must not hand over
            // usable sessions.
            TokenHash = hash,
            ExpiresAt = now.Add(RefreshTokenLifetime),
        });

        user.LastLoginAt = now;

        await database.SaveChangesAsync(cancellationToken);

        return token;
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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Turns a verified account into a session: the organizations it may act in,
/// a short-lived access token, and a renewal token recorded so the session
/// can later be closed.
///
/// Signing in and renewing both end here, so the two produce identical
/// sessions. If renewing built its token differently — an organization gained
/// or a role changed since — the difference would only surface as a
/// permission that works until the next renewal, or after it.
/// </summary>
public sealed class SessionIssuer(
    SportFrogDbContext database,
    JwtAccessTokenIssuer tokenIssuer,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock)
{
    /// <summary>
    /// How long a renewal token stays usable. Long enough that a person is
    /// not asked for their password daily, short enough that an abandoned
    /// session does not stay open forever.
    /// </summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public sealed record Organization(Guid Id, string Name, string Slug, MembershipRole Role);

    public sealed record Session(
        string AccessToken,
        string RefreshToken,
        int ExpiresInSeconds,
        IReadOnlyCollection<Organization> Organizations);

    /// <summary>
    /// Issues a session for an account whose identity has already been
    /// established — by password, or by a renewal token.
    /// </summary>
    /// <returns>
    /// The session, or <c>null</c> when the account can act nowhere. An
    /// account that belongs to no active organization has nothing to be given
    /// a token for.
    /// </returns>
    public async Task<Session?> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var organizations = await ReadOrganizationsAsync(userId, cancellationToken);

        if (organizations.Count == 0)
        {
            return null;
        }

        var accessToken = tokenIssuer.IssueForOrganizations(
            userId,
            [.. organizations.Select(organization =>
                new OrganizationAccess(organization.Id, organization.Role))]);

        var (refreshToken, hash) = RefreshTokenFactory.Create();

        database.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,

            // Only the digest is kept: a database dump must not hand over
            // usable sessions.
            TokenHash = hash,
            ExpiresAt = clock.GetUtcNow().Add(RefreshTokenLifetime),
        });

        await database.SaveChangesAsync(cancellationToken);

        return new Session(
            accessToken,
            refreshToken,
            (int)jwtOptions.Value.AccessTokenLifetime.TotalSeconds,
            organizations);
    }

    /// <summary>
    /// The organizations an account belongs to, and the role held in each.
    /// </summary>
    /// <remarks>
    /// This read precedes any organization context — there is nothing yet to
    /// establish one to, since which organizations exist for this account is
    /// the question. It works because the person is established instead: the
    /// own_memberships policy lets an account read the rows that name it.
    /// </remarks>
    private async Task<List<Organization>> ReadOrganizationsAsync(
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

        // A suspended or removed organization grants nothing, even though the
        // membership row outlives it.
        return
        [
            .. memberships
                .Where(membership =>
                    membership.Organization is { IsActive: true, DeletedAt: null })
                .Select(membership => new Organization(
                    membership.OrgId,
                    membership.Organization!.Name,
                    membership.Organization.Slug,
                    membership.Role)),
        ];
    }
}

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Whether a caller may create a new organization — a platform-level
/// question, not an organization-scoped one, so it does not fit
/// <see cref="RequireRoleExtensions.RequireRole"/> or
/// <see cref="Tenancy.OrganizationContext"/>: both answer "what may this
/// caller do inside the organization it is acting in", and creating an
/// organization does not act inside one — the one it creates does not exist
/// yet.
/// </summary>
/// <remarks>
/// Until self-service sign-up exists (see <see cref="Features.Organizations.RegisterOrganization"/>),
/// SportFrog itself onboards every organization by hand, from the one
/// account this seeds. <see cref="OrganizationSlug"/> names it rather than a
/// stored id because nothing about that organization is fixed except its
/// address — the same reason <c>RestrictPublicPortalToFrogtech</c> resolves
/// it by slug instead of a hardcoded id.
/// </remarks>
public static class PlatformAuthority
{
    /// <summary>
    /// The organization SportFrog itself operates under. Seeded by the
    /// <c>SeedPlatformOwner</c> migration together with its one account —
    /// never a customer's, and not one an operation reachable by an ordinary
    /// caller can create or rename.
    /// </summary>
    public const string OrganizationSlug = "frogtech-solutions";

    /// <summary>
    /// The platform organization's own id, or <c>null</c> when the seed
    /// migration never reached a database that has one.
    /// </summary>
    /// <remarks>
    /// Reads <c>organizations</c>, which carries no row-level security (see
    /// <c>ScopePublicReadsToPublishedCompetitions</c>'s own remarks on why),
    /// so this resolves correctly regardless of which organization's
    /// isolation context the caller is acting under — including none at all,
    /// which is exactly the case for every endpoint this type gates.
    /// </remarks>
    public static Task<Guid?> OrganizationIdAsync(
        SportFrogDbContext database, CancellationToken cancellationToken) =>
        database.Organizations
            .Where(organization => organization.Slug == OrganizationSlug)
            .Select(organization => (Guid?)organization.Id)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The role the token grants in the platform organization, or
    /// <c>null</c> when it grants none there — including when the platform
    /// organization itself does not exist yet, which a database the seed
    /// migration never reached answers the same way a token with no claim
    /// for it would.
    /// </summary>
    public static async Task<MembershipRole?> RoleForAsync(
        ClaimsPrincipal principal,
        SportFrogDbContext database,
        CancellationToken cancellationToken) =>
        await OrganizationIdAsync(database, cancellationToken) is { } organizationId
            ? JwtAccessTokenIssuer.FindRole(principal, organizationId)
            : null;

    /// <summary>
    /// Whether the token's role in the platform organization reaches
    /// <paramref name="minimum"/> — the one check every platform-only
    /// operation gates on, so each caller states what it requires without
    /// re-deriving how "reaches" is decided.
    /// </summary>
    public static async Task<bool> ReachesAsync(
        ClaimsPrincipal principal,
        SportFrogDbContext database,
        MembershipRole minimum,
        CancellationToken cancellationToken) =>
        await RoleForAsync(principal, database, cancellationToken) is { } role
            && role.Reaches(minimum);
}

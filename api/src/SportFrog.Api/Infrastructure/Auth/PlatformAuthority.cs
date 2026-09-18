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
    /// The role the token grants in the platform organization, or
    /// <c>null</c> when it grants none there — including when the platform
    /// organization itself does not exist yet, which a database the seed
    /// migration never reached answers the same way a token with no claim
    /// for it would.
    /// </summary>
    public static async Task<MembershipRole?> RoleForAsync(
        ClaimsPrincipal principal,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var platformOrganizationId = await database.Organizations
            .Where(organization => organization.Slug == OrganizationSlug)
            .Select(organization => (Guid?)organization.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return platformOrganizationId is { } organizationId
            ? JwtAccessTokenIssuer.FindRole(principal, organizationId)
            : null;
    }
}

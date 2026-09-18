using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Infrastructure.Auth;

/// <summary>
/// <see cref="PlatformAuthority.RoleForAsync"/> is what
/// <see cref="Features.Organizations.RegisterOrganization"/> gates on: it
/// must find the platform organization the <c>SeedPlatformOwner</c>
/// migration seeds, and answer exactly what the token grants there — never
/// what it grants anywhere else.
///
/// Isolation note: organizations carries no RLS policy, and the platform
/// organization is shared with every other test in this collection (it is
/// seeded once, by the migration, not per test). Every assertion below
/// either reads that one well-known row or an organization this test
/// created itself — never a count or a full-table read.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class PlatformAuthorityTests(SportFrogDatabaseFixture fixture)
{
    // 32 ASCII bytes: the minimum HS256 accepts without weakening the MAC.
    private const string SigningKey = "platform-authority-tests-key32!!";
    private const string Issuer = "sportfrog-api";
    private const string Audience = "sportfrog-clients";

    private readonly JwtAccessTokenIssuer _tokenIssuer =
        new(SigningKey, Issuer, Audience, TimeSpan.FromMinutes(15));

    [Fact]
    public async Task RoleForAsync_ReturnsTheRoleTheTokenGrants_InThePlatformOrganization()
    {
        await using var context = fixture.CreateAppContext();

        var platformOrganizationId = await context.Organizations
            .Where(organization => organization.Slug == PlatformAuthority.OrganizationSlug)
            .Select(organization => organization.Id)
            .SingleAsync();

        var token = _tokenIssuer.IssueForOrganizations(
            Guid.NewGuid(), [new OrganizationAccess(platformOrganizationId, MembershipRole.Admin)]);
        var principal = _tokenIssuer.Validate(token);

        var role = await PlatformAuthority.RoleForAsync(principal, context, CancellationToken.None);

        role.Should().Be(MembershipRole.Admin);
    }

    [Fact]
    public async Task RoleForAsync_ReturnsNull_WhenTheTokenGrantsNoRoleInThePlatformOrganization()
    {
        await using var context = fixture.CreateAppContext();

        // A role in some other organization — the point is that it is not
        // the platform one, not that the organization is unusual.
        var token = _tokenIssuer.IssueForOrganizations(
            Guid.NewGuid(), [new OrganizationAccess(Guid.NewGuid(), MembershipRole.Owner)]);
        var principal = _tokenIssuer.Validate(token);

        var role = await PlatformAuthority.RoleForAsync(principal, context, CancellationToken.None);

        role.Should().BeNull();
    }
}

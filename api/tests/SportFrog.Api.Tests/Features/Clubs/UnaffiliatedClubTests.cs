using AwesomeAssertions;
using SportFrog.Api.Features.Clubs;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Clubs;

/// <summary>
/// The one club, per organization, that an athlete with no delegation
/// enrolls under — created the first time an organization needs one.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class UnaffiliatedClubTests(SportFrogDatabaseFixture fixture)
{
    private async Task<Guid> SeedOrganizationAsync()
    {
        var orgId = Guid.NewGuid();

        await using var context = fixture.CreateAppContext();
        context.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        await context.SaveChangesAsync();

        return orgId;
    }

    [Fact]
    public async Task EnsureAsync_NoneExistsYet_CreatesOne()
    {
        var orgId = await SeedOrganizationAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var club = await new UnaffiliatedClub(context).EnsureAsync(orgId, CancellationToken.None);

        club.IsUnaffiliated.Should().BeTrue();
        club.Name.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EnsureAsync_CalledTwice_ReturnsTheSameClubBothTimes()
    {
        // The organization's second individual-sport enrollment must land
        // its athlete under the same unaffiliated club as the first, not a
        // second one competing for the same purpose.
        var orgId = await SeedOrganizationAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var service = new UnaffiliatedClub(context);

        var first = await service.EnsureAsync(orgId, CancellationToken.None);
        var second = await service.EnsureAsync(orgId, CancellationToken.None);

        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task EnsureAsync_TwoOrganizations_EachGetsItsOwn()
    {
        var orgOneId = await SeedOrganizationAsync();
        var orgTwoId = await SeedOrganizationAsync();

        await using var contextOne = fixture.CreateAppContext();
        await using var transactionOne = await contextOne.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(contextOne, orgOneId);
        var clubOne = await new UnaffiliatedClub(contextOne).EnsureAsync(orgOneId, CancellationToken.None);

        await using var contextTwo = fixture.CreateAppContext();
        await using var transactionTwo = await contextTwo.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(contextTwo, orgTwoId);
        var clubTwo = await new UnaffiliatedClub(contextTwo).EnsureAsync(orgTwoId, CancellationToken.None);

        clubOne.Id.Should().NotBe(clubTwo.Id);
        clubOne.OrgId.Should().Be(orgOneId);
        clubTwo.OrgId.Should().Be(orgTwoId);
    }
}

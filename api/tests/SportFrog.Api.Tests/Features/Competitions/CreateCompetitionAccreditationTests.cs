using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Competitions;

/// <summary>
/// A competition created through <c>POST /competitions</c> starts with the
/// football accreditation catalogue in the same save, against a real database.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CreateCompetitionAccreditationTests(SportFrogDatabaseFixture fixture)
{
    private static OrganizationContext ActingIn(CompetitionTenant tenant)
    {
        var organization = new OrganizationContext();
        organization.Establish(tenant.OrgId, MembershipRole.Admin, Guid.NewGuid(), ipAddress: null);

        return organization;
    }

    [Fact]
    public async Task CreatingAFootballCompetition_StartsItWithTheCatalogue()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var contract = new CompetitionContract(
            RulesetId: tenant.RulesetId,
            Name: "Copa Catálogo",
            Slug: $"copa-catalogo-{Guid.NewGuid():N}",
            Season: "2026",
            Format: "league",
            CaptureLevel: "basic",
            StartsOn: null,
            EndsOn: null,
            Settings: null);

        var result = await CreateCompetition.HandleAsync(
            contract, scope.Context, ActingIn(tenant), null!, CancellationToken.None);

        var created = Assert.IsType<Created<CreateCompetition.Response>>(result);
        var competitionId = created.Value!.Id;

        Assert.Equal(9, await scope.Context.AccreditationItems.CountAsync(item => item.CompetitionId == competitionId));
        Assert.Equal(2, await scope.Context.AccreditationCategories.CountAsync(category => category.CompetitionId == competitionId));
        Assert.Equal(18, await scope.Context.AccreditationCategoryItems.CountAsync(link => link.CompetitionId == competitionId));
    }
}

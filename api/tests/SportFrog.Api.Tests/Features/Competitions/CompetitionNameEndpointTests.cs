using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Competitions;

/// <summary>
/// The handlers behind <c>POST /competitions</c> and
/// <c>PUT /competitions/{id}</c>, called directly with a real database: what a
/// caller is told when the name they chose is already taken, and that the same
/// name is still fine where it should be.
/// </summary>
/// <remarks>
/// The picture store is never reached, since none of these contracts carry
/// portal pictures, so it is passed as null rather than standing up object
/// storage for a rule that has nothing to do with it.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CompetitionNameEndpointTests(SportFrogDatabaseFixture fixture)
{
    private static OrganizationContext ActingIn(CompetitionTenant tenant)
    {
        var organization = new OrganizationContext();
        organization.Establish(tenant.OrgId, MembershipRole.Admin, Guid.NewGuid(), ipAddress: null);

        return organization;
    }

    private static CompetitionContract Contract(
        CompetitionTenant tenant, string name, string slug) =>
        new(
            RulesetId: tenant.RulesetId,
            Name: name,
            Slug: slug,
            Season: "2026",
            Format: "league",
            CaptureLevel: "basic",
            StartsOn: null,
            EndsOn: null,
            Settings: null);

    private static Task<IResult> Create(
        CompetitionTenant tenant, CompetitionTenant.Scope scope, string name, string slug) =>
        CreateCompetition.HandleAsync(
            Contract(tenant, name, slug), scope.Context, ActingIn(tenant), null!, CancellationToken.None);

    private static Task<IResult> Update(
        CompetitionTenant tenant, CompetitionTenant.Scope scope, Guid id, string name, string slug) =>
        UpdateCompetition.HandleAsync(
            id, Contract(tenant, name, slug), scope.Context, null!, CancellationToken.None);

    private static void AssertConflict(IResult result, string expectedDetail)
    {
        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Contains(expectedDetail, problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task Create_WithAFreeName_Succeeds()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var result = await Create(tenant, scope, "La Liga 25/26", "la-liga-25-26");

        Assert.IsType<Created<CreateCompetition.Response>>(result);
    }

    [Theory]
    [InlineData("La Liga 25/26")]
    [InlineData("LA LIGA 25/26")]
    [InlineData("  la liga 25/26  ")]
    public async Task Create_WithANameAlreadyTaken_IsAConflict(string repeated)
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        Assert.IsType<Created<CreateCompetition.Response>>(
            await Create(tenant, scope, "La Liga 25/26", "la-liga-25-26"));

        var result = await Create(tenant, scope, repeated, "otra-direccion");

        AssertConflict(result, "con ese nombre");
    }

    [Fact]
    public async Task Create_WithASlugAlreadyTaken_StillAnswersWithTheAddressMessage()
    {
        // The address is checked first, so a request that repeats both is told
        // about the address -- the answer it always got.
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        Assert.IsType<Created<CreateCompetition.Response>>(
            await Create(tenant, scope, "La Liga 25/26", "la-liga-25-26"));

        var result = await Create(tenant, scope, "La Liga 26/27", "la-liga-25-26");

        AssertConflict(result, "esa dirección");
    }

    [Fact]
    public async Task Create_TheSameNameInAnotherOrganization_Succeeds()
    {
        var first = await CompetitionTenant.SeedAsync(fixture);
        var second = await CompetitionTenant.SeedAsync(fixture);

        await using (var scope = await first.OpenAsync(fixture))
        {
            Assert.IsType<Created<CreateCompetition.Response>>(
                await Create(first, scope, "La Liga 25/26", "la-liga-25-26"));
        }

        await using (var scope = await second.OpenAsync(fixture))
        {
            Assert.IsType<Created<CreateCompetition.Response>>(
                await Create(second, scope, "La Liga 25/26", "la-liga-25-26"));
        }
    }

    [Fact]
    public async Task Update_ToANameAnotherCompetitionHas_IsAConflict()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var other = tenant.NewCompetition("La Liga 25/26");
        var mine = tenant.NewCompetition("Copa 25/26");
        scope.Context.Competitions.AddRange(other, mine);
        await scope.Context.SaveChangesAsync();

        var result = await Update(tenant, scope, mine.Id, "la liga 25/26", mine.Slug);

        AssertConflict(result, "con ese nombre");
    }

    [Fact]
    public async Task Update_KeepingItsOwnName_Succeeds()
    {
        // The form resends the name it loaded. That is not a collision with
        // itself, and it is what every edit that leaves the name alone does.
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var mine = tenant.NewCompetition("La Liga 25/26");
        scope.Context.Competitions.Add(mine);
        await scope.Context.SaveChangesAsync();

        var result = await Update(tenant, scope, mine.Id, "La Liga 25/26", mine.Slug);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task Update_ToAFreeName_IsSaved()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var mine = tenant.NewCompetition("La Liga 25/26");
        scope.Context.Competitions.Add(mine);
        await scope.Context.SaveChangesAsync();

        var result = await Update(tenant, scope, mine.Id, "La Liga 26/27", mine.Slug);

        Assert.IsType<NoContent>(result);
        Assert.Equal(
            "La Liga 26/27",
            await scope.Context.Competitions
                .Where(competition => competition.Id == mine.Id)
                .Select(competition => competition.Name)
                .SingleAsync());
    }

    [Fact]
    public async Task Update_ToTheNameOfASoftDeletedCompetition_Succeeds()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var withdrawn = tenant.NewCompetition("La Liga 25/26");
        var mine = tenant.NewCompetition("Copa 25/26");
        scope.Context.Competitions.AddRange(withdrawn, mine);
        await scope.Context.SaveChangesAsync();

        withdrawn.DeletedAt = DateTimeOffset.UtcNow;
        await scope.Context.SaveChangesAsync();

        var result = await Update(tenant, scope, mine.Id, "La Liga 25/26", mine.Slug);

        Assert.IsType<NoContent>(result);
    }
}

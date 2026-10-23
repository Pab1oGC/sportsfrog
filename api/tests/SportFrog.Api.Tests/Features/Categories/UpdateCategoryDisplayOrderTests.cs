using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SportFrog.Api.Features.Categories;
using SportFrog.Api.Infrastructure.Caching;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Features.Competitions;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Tests.Features.Categories;

/// <summary>
/// The handler behind <c>PATCH /competitions/{competitionId}/categories/{id}/display-order</c>:
/// that it moves only the one column it is for, and nothing else a roster
/// was already accepted against.
/// </summary>
/// <remarks>
/// The public query cache is a real in-memory one rather than null, the same
/// choice <c>CompetitionNameEndpointTests</c> makes for <c>UpdateCompetition</c>:
/// this handler invalidates it unconditionally on every save, scoped to the
/// competition id under test.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class UpdateCategoryDisplayOrderTests(SportFrogDatabaseFixture fixture)
{
    private static readonly IPublicQueryCache PublicCache =
        new PublicQueryCache(new MemoryCache(new MemoryCacheOptions()));

    private static Category NewCategory(Guid orgId, Guid competitionId, string name, short displayOrder) => new()
    {
        Id = Guid.NewGuid(),
        OrgId = orgId,
        CompetitionId = competitionId,
        Name = name,
        DisplayOrder = displayOrder,
    };

    private static Task<IResult> Move(
        CompetitionTenant.Scope scope, Guid competitionId, Guid categoryId, short displayOrder) =>
        UpdateCategoryDisplayOrder.HandleAsync(
            competitionId,
            categoryId,
            new UpdateCategoryDisplayOrder.Request(displayOrder),
            scope.Context,
            PublicCache,
            CancellationToken.None);

    [Fact]
    public async Task Move_ChangesOnlyTheDisplayOrder()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2026");
        scope.Context.Competitions.Add(competition);

        // Everything eligibility already asks of a roster, set to something a
        // careless save would be easy to lose: if the handler went anywhere
        // near these fields, this is what would change.
        var category = NewCategory(tenant.OrgId, competition.Id, "Sub-15", displayOrder: 0);
        category.Gender = "F";
        category.MaxRosterSize = 20;
        category.RulesetId = tenant.RulesetId;
        scope.Context.Categories.Add(category);
        await scope.Context.SaveChangesAsync();

        var result = await Move(scope, competition.Id, category.Id, displayOrder: 1);

        Assert.IsType<NoContent>(result);

        var stored = await scope.Context.Categories
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == category.Id);

        Assert.Equal((short)1, stored.DisplayOrder);
        Assert.Equal("F", stored.Gender);
        Assert.Equal((short)20, stored.MaxRosterSize);
        Assert.Equal(tenant.RulesetId, stored.RulesetId);
    }

    [Fact]
    public async Task Move_OnAFinishedCompetition_StillSucceeds()
    {
        // The order is a label, not eligibility: UpdateCategory already
        // leaves it editable once a competition settles, and this endpoint
        // exists precisely so moving one does not have to ask the rest of
        // UpdateCategory's questions either.
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2025");
        competition.Status = CompetitionState.Finished;
        scope.Context.Competitions.Add(competition);

        var category = NewCategory(tenant.OrgId, competition.Id, "Primera", displayOrder: 0);
        scope.Context.Categories.Add(category);
        await scope.Context.SaveChangesAsync();

        var result = await Move(scope, competition.Id, category.Id, displayOrder: 2);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task Move_UnknownCategory_IsNotFound()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2026");
        scope.Context.Competitions.Add(competition);
        await scope.Context.SaveChangesAsync();

        var result = await Move(scope, competition.Id, Guid.NewGuid(), displayOrder: 1);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Move_ACategoryOfAnotherCompetition_IsNotFound()
    {
        // The route names the competition the category has to belong to, the
        // same guard UpdateCategory's own query makes: a category cannot be
        // reordered by naming a competition it is not actually part of.
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var mine = tenant.NewCompetition("Liga 2026");
        var other = tenant.NewCompetition("Copa 2026");
        scope.Context.Competitions.AddRange(mine, other);

        var category = NewCategory(tenant.OrgId, other.Id, "Primera", displayOrder: 0);
        scope.Context.Categories.Add(category);
        await scope.Context.SaveChangesAsync();

        var result = await Move(scope, mine.Id, category.Id, displayOrder: 1);

        Assert.IsType<NotFound>(result);
    }
}

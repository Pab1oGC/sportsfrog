using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Rulebook;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Features.Competitions;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Rulebook;

/// <summary>
/// <see cref="RulesetUsage.IsInUseAsync"/>: a reglamento counts as in use
/// when a <see cref="CategoryStage"/> overrides it, the same way it already
/// does for a competition or a category.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class RulesetUsageTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task IsInUseAsync_ARulesetOnlyAStageOverrides_IsTrue()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2026");
        scope.Context.Competitions.Add(competition);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CompetitionId = competition.Id,
            Name = "Primera",
        };
        scope.Context.Categories.Add(category);
        await scope.Context.SaveChangesAsync();

        var finalsRuleset = new Ruleset
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            SportCode = "football",
            Name = "Reglamento de la final",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        };
        scope.Context.Rulesets.Add(finalsRuleset);

        // Nada apunta a este reglamento salvo la fase -- ni la competencia
        // (apunta al de tenant.RulesetId) ni la categoría (sin override).
        scope.Context.CategoryStages.Add(new CategoryStage
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CategoryId = category.Id,
            Kind = StageKind.Bracket,
            RulesetId = finalsRuleset.Id,
        });
        await scope.Context.SaveChangesAsync();

        var usage = new RulesetUsage(scope.Context);

        Assert.True(await usage.IsInUseAsync(finalsRuleset.Id, CancellationToken.None));
    }

    [Fact]
    public async Task IsInUseAsync_ARulesetNoStageOverrides_IsFalse()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2026");
        scope.Context.Competitions.Add(competition);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CompetitionId = competition.Id,
            Name = "Primera",
        };
        scope.Context.Categories.Add(category);

        scope.Context.CategoryStages.Add(new CategoryStage
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CategoryId = category.Id,
            Kind = StageKind.League,
        });
        await scope.Context.SaveChangesAsync();

        var unused = new Ruleset
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            SportCode = "football",
            Name = "Sin usar",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        };
        scope.Context.Rulesets.Add(unused);
        await scope.Context.SaveChangesAsync();

        var usage = new RulesetUsage(scope.Context);

        Assert.False(await usage.IsInUseAsync(unused.Id, CancellationToken.None));
    }
}

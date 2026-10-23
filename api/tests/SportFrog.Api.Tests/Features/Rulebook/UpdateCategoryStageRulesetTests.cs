using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Categories;
using SportFrog.Api.Features.Rulebook;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Features.Competitions;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Rulebook;

/// <summary>
/// The handler behind <c>PATCH /categories/{categoryId}/stages/{kind}/ruleset</c>:
/// the volleyball case this endpoint exists for — a group stage still open
/// to its own reglamento while the bracket already has one, or the other
/// way around — and the refusal that stays per stage rather than per
/// category.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class UpdateCategoryStageRulesetTests(SportFrogDatabaseFixture fixture)
{
    private static Task<IResult> Update(
        CompetitionTenant.Scope scope, Guid categoryId, string kind, Guid? rulesetId) =>
        UpdateCategoryStageRuleset.HandleAsync(
            categoryId,
            kind,
            new UpdateCategoryStageRuleset.Request(rulesetId),
            scope.Context,
            new CategoryPolicy(scope.Context),
            new CategoryStagePolicy(scope.Context),
            CancellationToken.None);

    private sealed record SeededCategory(
        Category Category, CategoryStage Groups, CategoryStage Bracket,
        Guid HomeTeamId, Guid AwayTeamId, Guid RecorderId);

    /// <summary>
    /// A groups-format football category with its two stages already
    /// seeded, two real teams, and a user to record a match's result as.
    /// </summary>
    private static async Task<SeededCategory> SeedGroupsCategoryAsync(
        CompetitionTenant tenant, CompetitionTenant.Scope scope)
    {
        var competition = tenant.NewCompetition($"Liga {Guid.NewGuid():N}");
        competition.Format = CompetitionFormat.Groups;
        scope.Context.Competitions.Add(competition);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CompetitionId = competition.Id,
            Name = "Primera",
        };
        scope.Context.Categories.Add(category);

        var groupsStage = new CategoryStage
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CategoryId = category.Id,
            Kind = StageKind.Groups,
        };
        var bracketStage = new CategoryStage
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CategoryId = category.Id,
            Kind = StageKind.Bracket,
        };
        scope.Context.CategoryStages.AddRange(groupsStage, bracketStage);

        var clubA = new Club { Id = Guid.NewGuid(), OrgId = tenant.OrgId, Name = $"Club A {Guid.NewGuid():N}" };
        var clubB = new Club { Id = Guid.NewGuid(), OrgId = tenant.OrgId, Name = $"Club B {Guid.NewGuid():N}" };
        scope.Context.Clubs.AddRange(clubA, clubB);

        var teamA = new Team { Id = Guid.NewGuid(), OrgId = tenant.OrgId, ClubId = clubA.Id, CategoryId = category.Id, Name = "Equipo A" };
        var teamB = new Team { Id = Guid.NewGuid(), OrgId = tenant.OrgId, ClubId = clubB.Id, CategoryId = category.Id, Name = "Equipo B" };
        scope.Context.Teams.AddRange(teamA, teamB);

        // Sin org_id (sin RLS), misma precedencia que StandingsQueryTests:
        // solo hace falta para el FK matches.recorded_by de los partidos
        // "jugados" que estos tests siembran.
        var recorder = new User
        {
            Id = Guid.NewGuid(),
            Email = $"recorder-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            FullName = "Test Recorder",
        };
        scope.Context.Users.Add(recorder);

        await scope.Context.SaveChangesAsync();

        return new SeededCategory(category, groupsStage, bracketStage, teamA.Id, teamB.Id, recorder.Id);
    }

    private static Match NewPlayedMatch(
        Guid orgId, Guid competitionId, Guid categoryId, Guid homeTeamId, Guid awayTeamId,
        Guid recordedBy, string? phase) => new()
    {
        Id = Guid.NewGuid(),
        OrgId = orgId,
        CompetitionId = competitionId,
        CategoryId = categoryId,
        HomeTeamId = homeTeamId,
        AwayTeamId = awayTeamId,
        Phase = phase,
        Status = MatchState.Finished,
        HomeTotal = 2,
        AwayTotal = 1,
        RecordedBy = recordedBy,
    };

    private static async Task<Guid> SeedAnotherFootballRulesetAsync(CompetitionTenant.Scope scope, Guid orgId)
    {
        var ruleset = new Ruleset
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            SportCode = "football",
            Name = $"Reglamento {Guid.NewGuid():N}",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        };
        scope.Context.Rulesets.Add(ruleset);
        await scope.Context.SaveChangesAsync();

        return ruleset.Id;
    }

    [Fact]
    public async Task Update_WithNothingPlayedUnderTheStage_SetsItsOwnRuleset()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var seeded = await SeedGroupsCategoryAsync(tenant, scope);
        var finalsRuleset = await SeedAnotherFootballRulesetAsync(scope, tenant.OrgId);

        var result = await Update(scope, seeded.Category.Id, "bracket", finalsRuleset);

        Assert.IsType<NoContent>(result);
        Assert.Equal(
            finalsRuleset,
            (await scope.Context.CategoryStages.AsNoTracking()
                .SingleAsync(s => s.Id == seeded.Bracket.Id)).RulesetId);
    }

    [Fact]
    public async Task Update_ClearingToNull_RevertsToInheriting()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var seeded = await SeedGroupsCategoryAsync(tenant, scope);
        var bracket = await scope.Context.CategoryStages.SingleAsync(s => s.Id == seeded.Bracket.Id);
        bracket.RulesetId = await SeedAnotherFootballRulesetAsync(scope, tenant.OrgId);
        await scope.Context.SaveChangesAsync();

        var result = await Update(scope, seeded.Category.Id, "bracket", null);

        Assert.IsType<NoContent>(result);
        Assert.Null(
            (await scope.Context.CategoryStages.AsNoTracking()
                .SingleAsync(s => s.Id == seeded.Bracket.Id)).RulesetId);
    }

    [Fact]
    public async Task Update_ARulesetOfAnotherSport_IsRejected()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var seeded = await SeedGroupsCategoryAsync(tenant, scope);

        var volleyballRuleset = new Ruleset
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            SportCode = "volleyball",
            Name = "Reglamento de vóley",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 5, Label = "set" },
                Points = new Dictionary<string, int>(),
                Tiebreakers = [],
            },
        };
        scope.Context.Rulesets.Add(volleyballRuleset);
        await scope.Context.SaveChangesAsync();

        var result = await Update(scope, seeded.Category.Id, "bracket", volleyballRuleset.Id);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.IsType<HttpValidationProblemDetails>(problem.ProblemDetails).Errors.Should().ContainKey("RulesetId");
    }

    [Fact]
    public async Task Update_AStageThisCategoryDoesNotHave_IsNotFound()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        // Formato liga: la única fase que esta categoría tiene es "league".
        var competition = tenant.NewCompetition("Liga 2026");
        scope.Context.Competitions.Add(competition);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CompetitionId = competition.Id,
            Name = "Primera",
        };
        var leagueStage = new CategoryStage
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CategoryId = category.Id,
            Kind = StageKind.League,
        };
        scope.Context.Categories.Add(category);
        scope.Context.CategoryStages.Add(leagueStage);
        await scope.Context.SaveChangesAsync();

        var result = await Update(scope, category.Id, "bracket", Guid.NewGuid());

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Update_UnknownCategory_IsNotFound()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var result = await Update(scope, Guid.NewGuid(), "bracket", Guid.NewGuid());

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Update_AnUnknownKindSegment_IsNotFound()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var seeded = await SeedGroupsCategoryAsync(tenant, scope);

        var result = await Update(scope, seeded.Category.Id, "torneo-relampago", Guid.NewGuid());

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Update_WhenThatStageAlreadyPlayed_IsAConflict()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var seeded = await SeedGroupsCategoryAsync(tenant, scope);

        // Un partido de la llave: fase no nula, igual que Bracket.Phase ya
        // la deja -- ver CompetitionStages.KindOfMatch.
        scope.Context.Matches.Add(NewPlayedMatch(
            tenant.OrgId, seeded.Category.CompetitionId, seeded.Category.Id,
            seeded.HomeTeamId, seeded.AwayTeamId, seeded.RecorderId, phase: "final"));
        await scope.Context.SaveChangesAsync();

        var result = await Update(
            scope, seeded.Category.Id, "bracket", await SeedAnotherFootballRulesetAsync(scope, tenant.OrgId));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Null(
            (await scope.Context.CategoryStages.AsNoTracking()
                .SingleAsync(s => s.Id == seeded.Bracket.Id)).RulesetId);
    }

    [Fact]
    public async Task Update_SettingTheSameRulesetAgain_IsAllowedEvenAfterPlaying()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var seeded = await SeedGroupsCategoryAsync(tenant, scope);
        var current = await SeedAnotherFootballRulesetAsync(scope, tenant.OrgId);
        var bracket = await scope.Context.CategoryStages.SingleAsync(s => s.Id == seeded.Bracket.Id);
        bracket.RulesetId = current;
        scope.Context.Matches.Add(NewPlayedMatch(
            tenant.OrgId, seeded.Category.CompetitionId, seeded.Category.Id,
            seeded.HomeTeamId, seeded.AwayTeamId, seeded.RecorderId, phase: "final"));
        await scope.Context.SaveChangesAsync();

        var result = await Update(scope, seeded.Category.Id, "bracket", current);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task Update_TheGroupStageAlreadyPlayed_StillLeavesTheBracketsRulesetOpen()
    {
        // El caso de vóley: la fase de grupos ya se jugó, y eso no bloquea
        // declarar el reglamento de la llave -- fases distintas, preguntas
        // distintas.
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var seeded = await SeedGroupsCategoryAsync(tenant, scope);

        // Un partido de grupos: fase nula -- ver CompetitionStages.KindOfMatch.
        scope.Context.Matches.Add(NewPlayedMatch(
            tenant.OrgId, seeded.Category.CompetitionId, seeded.Category.Id,
            seeded.HomeTeamId, seeded.AwayTeamId, seeded.RecorderId, phase: null));
        await scope.Context.SaveChangesAsync();

        var finalsRuleset = await SeedAnotherFootballRulesetAsync(scope, tenant.OrgId);
        var result = await Update(scope, seeded.Category.Id, "bracket", finalsRuleset);

        Assert.IsType<NoContent>(result);
        Assert.Equal(
            finalsRuleset,
            (await scope.Context.CategoryStages.AsNoTracking()
                .SingleAsync(s => s.Id == seeded.Bracket.Id)).RulesetId);
    }
}

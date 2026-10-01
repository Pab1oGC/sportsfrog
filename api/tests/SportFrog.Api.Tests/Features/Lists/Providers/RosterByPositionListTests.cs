using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>
/// How <see cref="RosterByPositionList"/> turns one of
/// <see cref="ChampionResolver"/>'s three outcomes into a
/// <see cref="ListTable"/> — <see cref="ChampionResolverTests"/> already
/// covers which outcome each format and position produces, so this seeds
/// just enough of each to pin the provider's own translation: not found
/// stays 404, undecided becomes an empty list carrying its reason, and a
/// resolved position becomes that team's roster.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class RosterByPositionListTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(Guid OrgId, Guid CategoryId, Guid ChampionTeamId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>A league of two teams, decided, with the champion carrying one numbered player.</summary>
    private async Task<Fixture> SeedDecidedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Test User",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubAId, OrgId = orgId, Name = "Club A" });
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId, OrgId = orgId, SportCode = "football", Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId, OrgId = orgId, SportCode = "football", RulesetId = rulesetId,
            Name = "Copa de Prueba", Slug = $"copa-{competitionId:N}", Season = "2026",
            Format = CompetitionFormat.League, Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Primera" });
        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = categoryId, Name = "Equipo B" });

        setup.Athletes.Add(new Athlete
        {
            Id = athleteId, OrgId = orgId, FirstName = "Juan", LastName = "Diaz",
            DocumentId = $"doc-{athleteId:N}", BirthDate = new DateOnly(2000, 1, 1),
        });

        await setup.SaveChangesAsync();

        setup.RosterEntries.Add(new RosterEntry
        {
            Id = Guid.NewGuid(), OrgId = orgId, TeamId = teamAId, AthleteId = athleteId, JerseyNumber = 9,
        });

        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = teamAId, AwayTeamId = teamBId, HomeTotal = 2, AwayTotal = 1,
            Status = MatchState.Finished, RecordedBy = userId,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, categoryId, teamAId);
    }

    /// <summary>The same league, with nobody having played yet — resolvable category, unresolved position.</summary>
    private async Task<Fixture> SeedUndecidedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Test User",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubAId, OrgId = orgId, Name = "Club A" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId, OrgId = orgId, SportCode = "football", Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId, OrgId = orgId, SportCode = "football", RulesetId = rulesetId,
            Name = "Copa de Prueba", Slug = $"copa-{competitionId:N}", Season = "2026",
            Format = CompetitionFormat.League, Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Primera" });
        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = "Equipo A" });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, categoryId, Guid.Empty);
    }

    private async Task<Session> OpenAsync(Guid orgId)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        return new Session(context, transaction);
    }

    private static ListScope Scope(Guid categoryId, int position) => new(new Dictionary<string, string>
    {
        ["categoryId"] = categoryId.ToString(),
        ["position"] = position.ToString(),
    });

    [Fact]
    public async Task LoadAsync_UnknownCategory_ReturnsNull()
    {
        var fx = await SeedDecidedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var table = await new RosterByPositionList().LoadAsync(
            Scope(Guid.NewGuid(), 1), session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_MissingPositionInScope_ReturnsNull()
    {
        var fx = await SeedDecidedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var scope = new ListScope(new Dictionary<string, string> { ["categoryId"] = fx.CategoryId.ToString() });
        var table = await new RosterByPositionList().LoadAsync(scope, session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_ResolvedPosition_TitlesItByTheChampionAndCarriesTheirRoster()
    {
        var fx = await SeedDecidedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var table = await new RosterByPositionList().LoadAsync(Scope(fx.CategoryId, 1), session.Context, CancellationToken.None);

        table!.Title.Should().Be("Plantel del campeón — Primera");
        table.Subtitle.Should().BeNull();
        table.Sections.Should().ContainSingle();
        table.Sections[0].Rows.Should().ContainSingle(row => row[0]!.Equals("9") && row[1]!.Equals("Diaz, Juan"));
    }

    [Fact]
    public async Task LoadAsync_RunnerUp_TitlesItAsSubcampeon()
    {
        var fx = await SeedDecidedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var table = await new RosterByPositionList().LoadAsync(Scope(fx.CategoryId, 2), session.Context, CancellationToken.None);

        table!.Title.Should().Be("Plantel del subcampeón — Primera");
    }

    [Fact]
    public async Task LoadAsync_FourthPlace_UsesTheOrdinalFallbackLabel()
    {
        var fx = await SeedDecidedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var table = await new RosterByPositionList().LoadAsync(Scope(fx.CategoryId, 4), session.Context, CancellationToken.None);

        table!.Title.Should().Be("Plantel del 4° puesto — Primera");
    }

    [Fact]
    public async Task LoadAsync_UndecidedPosition_IsAnEmptyListCarryingTheReasonAsSubtitle()
    {
        var fx = await SeedUndecidedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var table = await new RosterByPositionList().LoadAsync(Scope(fx.CategoryId, 1), session.Context, CancellationToken.None);

        table.Should().NotBeNull();
        table!.Sections.Should().BeEmpty();
        table.Subtitle.Should().Be("Todavía no hay resultados cargados en esta categoría.");
    }
}

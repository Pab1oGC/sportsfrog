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
using SportFrog.Domain.Standings;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>
/// The pure shaping <see cref="StandingsList"/> does on its own — which
/// columns a table gets and what one row carries — pinned directly, without
/// a database, the same way <c>Leaderboard</c>'s own tie-sharing arithmetic
/// is pinned without one. <see cref="StandingsList.LoadAsync"/> itself, the
/// part that actually reads a category, needs one: it is covered in the
/// second half of this file, against a real database.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class StandingsListTests(SportFrogDatabaseFixture fixture)
{
    private static StandingsRow Row(string team, int played, int won, int drawn, int lost, int scoreFor, int scoreAgainst, int points) =>
        new()
        {
            TeamId = Guid.NewGuid(),
            TeamName = team,
            Played = played,
            Won = won,
            Drawn = drawn,
            Lost = lost,
            ScoreFor = scoreFor,
            ScoreAgainst = scoreAgainst,
            Points = points,
        };

    [Fact]
    public void Columns_AllowsDraw_IncludesEmpatados()
    {
        StandingsList.Columns(allowsDraw: true).Select(column => column.Header)
            .Should().ContainInOrder("Pos.", "Equipo", "PJ", "PG", "PE", "PP", "GF", "GC", "Dif.", "Pts.");
    }

    [Fact]
    public void Columns_DoesNotAllowDraw_OmitsEmpatados()
    {
        StandingsList.Columns(allowsDraw: false).Select(column => column.Header)
            .Should().ContainInOrder("Pos.", "Equipo", "PJ", "PG", "PP", "GF", "GC", "Dif.", "Pts.")
            .And.NotContain("PE");
    }

    [Fact]
    public void Row_AllowsDraw_CarriesOneValuePerColumn()
    {
        var row = Row("Equipo A", played: 5, won: 3, drawn: 1, lost: 1, scoreFor: 10, scoreAgainst: 6, points: 10);

        var cells = StandingsList.Row(index: 0, row, allowsDraw: true);

        cells.Should().Equal(1, "Equipo A", 5, 3, 1, 1, 10, 6, 4, 10);
    }

    [Fact]
    public void Row_DoesNotAllowDraw_OmitsDrawnValue()
    {
        var row = Row("Equipo A", played: 5, won: 3, drawn: 0, lost: 2, scoreFor: 10, scoreAgainst: 6, points: 9);

        var cells = StandingsList.Row(index: 0, row, allowsDraw: false);

        cells.Should().Equal(1, "Equipo A", 5, 3, 2, 10, 6, 4, 9);
    }

    [Fact]
    public void Row_Position_IsIndexPlusOne()
    {
        var row = Row("Equipo B", played: 1, won: 0, drawn: 0, lost: 1, scoreFor: 0, scoreAgainst: 1, points: 0);

        StandingsList.Row(index: 2, row, allowsDraw: true)[0].Should().Be(3);
    }

    // ---- LoadAsync, against a real database ------------------------------

    private sealed record Fixture(Guid OrgId, Guid CategoryId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>Two teams, one finished match — enough for a one-group table with a winner and a loser.</summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
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
            Id = rulesetId,
            OrgId = orgId,
            SportCode = "football",
            Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId,
            OrgId = orgId,
            SportCode = "football",
            RulesetId = rulesetId,
            Name = "Copa de Prueba",
            Slug = $"copa-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Primera" });
        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = categoryId, Name = "Equipo B" });

        await setup.SaveChangesAsync();

        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            CompetitionId = competitionId,
            CategoryId = categoryId,
            HomeTeamId = teamAId,
            AwayTeamId = teamBId,
            HomeTotal = 2,
            AwayTotal = 1,
            Status = MatchState.Finished,
            RecordedBy = userId,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, categoryId);
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    [Fact]
    public async Task LoadAsync_UnknownCategory_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new StandingsList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = Guid.NewGuid().ToString() }),
            session.Context,
            CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_NoCategoryIdInScope_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new StandingsList().LoadAsync(ListScope.Empty, session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_KnownCategory_TitleNamesTheCategoryAndWinnerLeadsTheTable()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new StandingsList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = fx.CategoryId.ToString() }),
            session.Context,
            CancellationToken.None);

        table!.Title.Should().Be("Tabla de posiciones — Primera");

        var rows = table.Sections.Single().Rows;
        rows[0].Should().Equal(1, "Equipo A", 1, 1, 0, 0, 2, 1, 1, 3);
        rows[1].Should().Equal(2, "Equipo B", 1, 0, 0, 1, 1, 2, -1, 0);
    }
}

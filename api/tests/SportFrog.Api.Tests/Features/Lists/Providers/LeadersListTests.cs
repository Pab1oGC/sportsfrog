using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
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

/// <summary>Whether <see cref="LeadersList"/> actually reads a category's scoring boards — a
/// query that compiles can still fail the moment EF Core tries to translate it, which only
/// shows up against a real database.</summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class LeadersListTests(SportFrogDatabaseFixture fixture)
{
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

    /// <summary>One player, one goal, one yellow card — enough to populate both a scoring
    /// board (what <see cref="LeadersList"/> exports) and a disciplinary one (what it must
    /// leave out).</summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var opponentClubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var opponentTeamId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var rosterEntryId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        var goalMetricId = await setup.SportMetrics
            .AsNoTracking()
            .Where(metric => metric.SportCode == "football" && metric.Code == "goal")
            .Select(metric => metric.Id)
            .SingleAsync();
        var cardMetricId = await setup.SportMetrics
            .AsNoTracking()
            .Where(metric => metric.SportCode == "football" && metric.Code == "yellow_card")
            .Select(metric => metric.Id)
            .SingleAsync();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Test User",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club A" });
        setup.Clubs.Add(new Club { Id = opponentClubId, OrgId = orgId, Name = "Club B" });

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
        setup.Teams.Add(new Team { Id = teamId, OrgId = orgId, ClubId = clubId, CategoryId = categoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = opponentTeamId, OrgId = orgId, ClubId = opponentClubId, CategoryId = categoryId, Name = "Equipo B" });

        setup.Athletes.Add(new Athlete
        {
            Id = athleteId, OrgId = orgId, FirstName = "Juan", LastName = "Diaz",
            DocumentId = $"doc-{athleteId:N}", BirthDate = new DateOnly(2000, 1, 1),
        });

        await setup.SaveChangesAsync();

        setup.RosterEntries.Add(new RosterEntry
        {
            Id = rosterEntryId, OrgId = orgId, TeamId = teamId, AthleteId = athleteId, JerseyNumber = 9,
        });

        setup.Matches.Add(new Match
        {
            Id = matchId,
            OrgId = orgId,
            CompetitionId = competitionId,
            CategoryId = categoryId,
            HomeTeamId = teamId,
            AwayTeamId = opponentTeamId,
            HomeTotal = 2,
            AwayTotal = 0,
            Status = MatchState.Finished,
            RecordedBy = userId,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });

        await setup.SaveChangesAsync();

        setup.PlayerEvents.Add(new PlayerEvent
        {
            Id = Guid.NewGuid(), OrgId = orgId, MatchId = matchId, RosterEntryId = rosterEntryId,
            MetricId = goalMetricId, Quantity = 2,
        });
        setup.PlayerEvents.Add(new PlayerEvent
        {
            Id = Guid.NewGuid(), OrgId = orgId, MatchId = matchId, RosterEntryId = rosterEntryId,
            MetricId = cardMetricId, Quantity = 1,
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

        var table = await new LeadersList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = Guid.NewGuid().ToString() }),
            session.Context,
            CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_KnownCategory_OnlyExportsTheScoringBoard()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new LeadersList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = fx.CategoryId.ToString() }),
            session.Context,
            CancellationToken.None);

        table!.Title.Should().Be("Máximos anotadores — Primera");
        table.Sections.Should().ContainSingle(section => section.Label == "Gol");
        table.Sections.Should().NotContain(section => section.Label == "Tarjeta amarilla");
    }

    [Fact]
    public async Task LoadAsync_KnownCategory_RowCarriesTheScorersTotal()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new LeadersList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = fx.CategoryId.ToString() }),
            session.Context,
            CancellationToken.None);

        var row = table!.Sections.Single(section => section.Label == "Gol").Rows.Single();

        row.Should().Equal(1, "Diaz, Juan", "9", "Equipo A", 2);
    }
}

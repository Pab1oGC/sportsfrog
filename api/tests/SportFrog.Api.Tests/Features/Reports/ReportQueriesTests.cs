using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Features.Reports;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Reports;

/// <summary>
/// Whether <see cref="TeamReportQuery"/> and <see cref="AthleteReportQuery"/>
/// actually run — a query that compiles can still fail the moment EF Core
/// tries to translate it, and that only shows up against a real database.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ReportQueriesTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(
        Guid OrgId, Guid TeamAId, Guid TeamBId, Guid AthleteAId, Guid RosterEntryAId, Guid MatchId);

    // Nothing in this fixture ever sets a competition's LogoKey, so
    // ObjectStore.ReadAsync is never actually reached — the client it would
    // call through stays null on purpose, the same reason a policy test
    // never wires a real payment gateway to exercise a code path that never
    // calls one.
    private static readonly ObjectStore Store = new(
        null!, Options.Create(new StorageOptions()), NullLogger<ObjectStore>.Instance);

    // None of this fixture's athletes ever get a PhotoKey either, so
    // AthletePhoto.BytesAsync always takes its "no key" branch and returns
    // the default avatar without ever touching Store or OrganizationContext
    // — the same reasoning as Store itself, one line up.
    private static readonly AthletePhoto Photos = new(Store, null!, NullLogger<AthletePhoto>.Instance);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>
    /// A finished match, one goal-scorer, and a team that leads its own
    /// (one-team) group by winning it — enough to exercise every branch a
    /// team-sport report reads: standing, fixture list, and own leaders.
    /// </summary>
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
        var athleteAId = Guid.NewGuid();
        var rosterEntryAId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        // The sport catalog — sports_metrics included — is seeded once by
        // migration and shared by every organization, not written per test:
        // "goal" for football already exists (20260813120100_SeedCatalog),
        // and inserting a second one collides with its own unique index.
        var goalMetricId = await setup.SportMetrics
            .AsNoTracking()
            .Where(metric => metric.SportCode == "football" && metric.Code == "goal")
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

        setup.Athletes.Add(new Athlete
        {
            Id = athleteAId,
            OrgId = orgId,
            FirstName = "Juan",
            LastName = "Pérez",
            DocumentId = $"doc-{athleteAId:N}",
            BirthDate = new DateOnly(2000, 1, 1),
        });

        await setup.SaveChangesAsync();

        setup.RosterEntries.Add(new RosterEntry
        {
            Id = rosterEntryAId, OrgId = orgId, TeamId = teamAId, AthleteId = athleteAId, JerseyNumber = 9,
        });

        setup.Matches.Add(new Match
        {
            Id = matchId,
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

        setup.PlayerEvents.Add(new PlayerEvent
        {
            Id = Guid.NewGuid(), OrgId = orgId, MatchId = matchId, RosterEntryId = rosterEntryAId,
            MetricId = goalMetricId, Quantity = 2,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, teamAId, teamBId, athleteAId, rosterEntryAId, matchId);
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    [Fact]
    public async Task TeamReport_TeamSport_ReturnsStanding_Matches_AndOwnLeaders()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var report = await TeamReportQuery.ForTeamAsync(session.Context, Store, fx.TeamAId, CancellationToken.None);

        report.Should().NotBeNull();
        report!.IsJudged.Should().BeFalse();
        report.StandingsPosition.Should().Be(1);
        report.Standing.Should().NotBeNull();
        report.Standing!.Points.Should().Be(3);
        report.Matches.Should().ContainSingle(match => match.OpponentName == "Equipo B" && match.Outcome == "Ganado");
        report.Leaders.Should().ContainSingle(leader => leader.MetricLabel == "Gol" && leader.Total == 2);
    }

    [Fact]
    public async Task TeamReport_LosingTeam_ShowsLostOutcomeAndNoLeadersOfItsOwn()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var report = await TeamReportQuery.ForTeamAsync(session.Context, Store, fx.TeamBId, CancellationToken.None);

        report.Should().NotBeNull();
        report!.Matches.Should().ContainSingle(match => match.OpponentName == "Equipo A" && match.Outcome == "Perdido");
        report.Leaders.Should().BeEmpty();
    }

    [Fact]
    public async Task TeamReport_UnknownTeam_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var report = await TeamReportQuery.ForTeamAsync(session.Context, Store, Guid.NewGuid(), CancellationToken.None);

        report.Should().BeNull();
    }

    [Fact]
    public async Task AthleteReport_TeamSport_ReturnsOneEntryWithMatchesAndMetrics()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var report = await AthleteReportQuery.ForAthleteAsync(session.Context, Store, Photos, fx.AthleteAId, CancellationToken.None);

        report.Should().NotBeNull();
        report!.FirstName.Should().Be("Juan");
        report.Entries.Should().ContainSingle();

        var entry = report.Entries[0];
        entry.IsJudged.Should().BeFalse();
        entry.Withdrawn.Should().BeFalse();
        entry.Matches.Should().ContainSingle(match => match.Outcome == "Ganado");
        entry.Metrics.Should().ContainSingle(metric => metric.MetricLabel == "Gol" && metric.Total == 2);
    }

    [Fact]
    public async Task AthleteReport_UnknownAthlete_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var report = await AthleteReportQuery.ForAthleteAsync(session.Context, Store, Photos, Guid.NewGuid(), CancellationToken.None);

        report.Should().BeNull();
    }
}

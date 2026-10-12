using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Infrastructure.Tenancy;

/// <summary>
/// What a route value actually resolves to, for each
/// <see cref="ScopeTargetKind"/> — the fact <see cref="RequireScopeExtensions"/>
/// checks a caller's scopes against. A category resolves to its competition
/// and nothing else; a team resolves to both the competition its category
/// belongs to and the club that fields it, since it genuinely belongs to
/// both at once; and an id that names no row of the given kind resolves to
/// nothing, the same answer whether the row never existed or belongs to
/// another organization entirely (row-level security, not this class, is
/// what makes the second case true).
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ScopeResolverTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task Competition_kind_resolves_to_itself_with_no_query()
    {
        // No row of any kind has to exist for this one: the route value IS
        // the competition id.
        var fx = await SeedAsync();
        var resolved = await ResolveAsync(fx.OrgId, ScopeTargetKind.Competition, fx.CompetitionId);

        resolved.Should().Be(new ResolvedScopeTarget(fx.CompetitionId, null));
    }

    [Fact]
    public async Task Club_kind_resolves_to_itself_with_no_query()
    {
        var fx = await SeedAsync();
        var resolved = await ResolveAsync(fx.OrgId, ScopeTargetKind.Club, fx.ClubId);

        resolved.Should().Be(new ResolvedScopeTarget(null, fx.ClubId));
    }

    [Fact]
    public async Task Category_kind_resolves_to_its_competition_and_no_club()
    {
        var fx = await SeedAsync();
        var resolved = await ResolveAsync(fx.OrgId, ScopeTargetKind.Category, fx.CategoryId);

        resolved.Should().Be(new ResolvedScopeTarget(fx.CompetitionId, null));
    }

    [Fact]
    public async Task Team_kind_resolves_to_both_its_competition_and_its_club()
    {
        var fx = await SeedAsync();
        var resolved = await ResolveAsync(fx.OrgId, ScopeTargetKind.Team, fx.TeamId);

        resolved.Should().Be(new ResolvedScopeTarget(fx.CompetitionId, fx.ClubId));
    }

    [Fact]
    public async Task Match_kind_resolves_to_its_competition_and_no_club()
    {
        var fx = await SeedAsync();
        var resolved = await ResolveAsync(fx.OrgId, ScopeTargetKind.Match, fx.MatchId);

        resolved.Should().Be(new ResolvedScopeTarget(fx.CompetitionId, null));
    }

    [Fact]
    public async Task Performance_kind_resolves_to_its_competition_and_no_club()
    {
        var fx = await SeedAsync();
        var resolved = await ResolveAsync(fx.OrgId, ScopeTargetKind.Performance, fx.PerformanceId);

        resolved.Should().Be(new ResolvedScopeTarget(fx.CompetitionId, null));
    }

    [Fact]
    public async Task PlayerEvent_kind_resolves_to_its_matchs_competition_and_no_club()
    {
        var fx = await SeedAsync();
        var resolved = await ResolveAsync(fx.OrgId, ScopeTargetKind.PlayerEvent, fx.PlayerEventId);

        resolved.Should().Be(new ResolvedScopeTarget(fx.CompetitionId, null));
    }

    [Fact]
    public async Task RosterEntry_kind_resolves_to_both_its_teams_competition_and_club()
    {
        var fx = await SeedAsync();
        var resolved = await ResolveAsync(fx.OrgId, ScopeTargetKind.RosterEntry, fx.RosterEntryId);

        resolved.Should().Be(new ResolvedScopeTarget(fx.CompetitionId, fx.ClubId));
    }

    [Theory]
    [InlineData(ScopeTargetKind.Category)]
    [InlineData(ScopeTargetKind.Team)]
    [InlineData(ScopeTargetKind.Match)]
    [InlineData(ScopeTargetKind.Performance)]
    [InlineData(ScopeTargetKind.PlayerEvent)]
    [InlineData(ScopeTargetKind.RosterEntry)]
    public async Task An_id_naming_no_row_of_that_kind_resolves_to_null(ScopeTargetKind kind)
    {
        var fx = await SeedAsync();

        var resolved = await ResolveAsync(fx.OrgId, kind, Guid.NewGuid());

        resolved.Should().BeNull();
    }

    private async Task<ResolvedScopeTarget?> ResolveAsync(Guid organizationId, ScopeTargetKind kind, Guid id)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, organizationId);

        var resolver = new ScopeResolver(context);
        var result = await resolver.ResolveAsync(kind, id, CancellationToken.None);

        await transaction.CommitAsync();
        return result;
    }

    private sealed record Fixture(
        Guid OrgId,
        Guid ClubId,
        Guid CompetitionId,
        Guid CategoryId,
        Guid TeamId,
        Guid MatchId,
        Guid PerformanceId,
        Guid PlayerEventId,
        Guid RosterEntryId);

    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var performanceId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club de Prueba" });

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
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category
        {
            Id = categoryId,
            OrgId = orgId,
            CompetitionId = competitionId,
            Name = "Categoria",
        });

        setup.Teams.Add(new Team
        {
            Id = teamId,
            OrgId = orgId,
            ClubId = clubId,
            CategoryId = categoryId,
            Name = "Equipo de Prueba",
        });

        // uq_teams_club_category allows only one team per (club, category):
        // the opponent needs a club of its own, not fx's.
        var opponentClubId = Guid.NewGuid();
        var opponentTeamId = Guid.NewGuid();
        setup.Clubs.Add(new Club { Id = opponentClubId, OrgId = orgId, Name = "Club Rival" });
        setup.Teams.Add(new Team
        {
            Id = opponentTeamId,
            OrgId = orgId,
            ClubId = opponentClubId,
            CategoryId = categoryId,
            Name = "Equipo Rival",
        });

        setup.Matches.Add(new Match
        {
            Id = matchId,
            OrgId = orgId,
            CompetitionId = competitionId,
            CategoryId = categoryId,
            // ck_home_slot_defined/ck_away_slot_defined require a team or a
            // bracket source match on each side -- a fixture with neither
            // has not named both sides yet.
            HomeTeamId = teamId,
            AwayTeamId = opponentTeamId,
        });

        setup.Performances.Add(new Performance
        {
            Id = performanceId,
            OrgId = orgId,
            CompetitionId = competitionId,
            CategoryId = categoryId,
            TeamId = teamId,
        });

        var athleteId = Guid.NewGuid();
        var rosterEntryId = Guid.NewGuid();
        setup.Athletes.Add(new Athlete
        {
            Id = athleteId,
            OrgId = orgId,
            FirstName = "Atleta",
            LastName = "De Prueba",
            DocumentId = $"doc-{athleteId:N}",
        });
        setup.RosterEntries.Add(new RosterEntry
        {
            Id = rosterEntryId,
            OrgId = orgId,
            TeamId = teamId,
            AthleteId = athleteId,
        });

        await setup.SaveChangesAsync();

        // The seeded catalog (SeedCatalog), not this fixture, owns football's
        // metrics -- read the id rather than inventing a row for one.
        var metricId = await setup.SportMetrics
            .Where(metric => metric.SportCode == "football" && metric.Code == "goal")
            .Select(metric => metric.Id)
            .SingleAsync();

        var playerEventId = Guid.NewGuid();
        setup.PlayerEvents.Add(new PlayerEvent
        {
            Id = playerEventId,
            OrgId = orgId,
            MatchId = matchId,
            RosterEntryId = rosterEntryId,
            MetricId = metricId,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(
            orgId, clubId, competitionId, categoryId, teamId, matchId, performanceId, playerEventId, rosterEntryId);
    }
}

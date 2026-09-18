using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Public;

/// <summary>
/// The <c>published_matches</c> row-level policy, proven the same way every
/// other public-role grant in this codebase is — as a real
/// <c>sportfrog_public</c> connection, not as an assertion about SQL text.
/// See <see cref="SportFrog.Api.Tests.Features.Performances.PerformancesPublicReadTests"/>
/// for why: a missing policy fails closed (the landing hero's ticker reads
/// nothing, which is boring but safe) and a policy wider than intended fails
/// open (an organization's private match leaks to a visitor who never opened
/// its page) — this asserts the actual boundary, not the migration's SQL.
///
/// list_public_recent_results is exercised directly here too, now that
/// RemovePublicPortalOrgRestriction dropped the one-organization-slug
/// restriction it briefly carried — a freshly seeded test organization
/// reaches it like any other, no need to write into the shared
/// frogtech-solutions fixture data to prove it works.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class PublicMatchesReadTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Seeded(Guid OrgId, Guid MatchId, string HomeTeamName, string AwayTeamName);

    private async Task<Seeded> SeedMatchAsync(
        bool competitionIsPublic,
        MatchState status = MatchState.Scheduled,
        int? homeTotal = null,
        int? awayTotal = null)
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var homeTeamName = $"Equipo A {teamAId:N}";
        var awayTeamName = $"Equipo B {teamBId:N}";

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
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = CompetitionFormat.League,
            IsPublic = competitionIsPublic,
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Categoria" });

        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = homeTeamName });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = categoryId, Name = awayTeamName });

        setup.Matches.Add(new Match
        {
            Id = matchId,
            OrgId = orgId,
            CompetitionId = competitionId,
            CategoryId = categoryId,
            HomeTeamId = teamAId,
            AwayTeamId = teamBId,
            Status = status,
            HomeTotal = homeTotal,
            AwayTotal = awayTotal,
            RecordedBy = status is MatchState.Finished or MatchState.Walkover ? userId : null,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Seeded(orgId, matchId, homeTeamName, awayTeamName);
    }

    /// <summary>
    /// Reads one row as <c>sportfrog_public</c>, the same connection shape
    /// <c>list_public_recent_results</c> itself runs under: no session, an
    /// isolation context set (or not) with <c>SET LOCAL</c>, gone at the end
    /// of the transaction.
    /// </summary>
    private async Task<int> CountAsPublicAsync(Guid? organizationContext, Guid matchId)
    {
        await using var connection = new NpgsqlConnection(fixture.PublicConnectionString);
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        if (organizationContext is { } organizationId)
        {
            await using var setContext = new NpgsqlCommand(
                "SELECT set_config('app.current_org', $1, true)", connection, transaction);
            setContext.Parameters.Add(new NpgsqlParameter { Value = organizationId.ToString() });
            await setContext.ExecuteNonQueryAsync();
        }

        await using var query = new NpgsqlCommand(
            "SELECT count(*) FROM matches WHERE id = $1", connection, transaction);
        query.Parameters.Add(new NpgsqlParameter { Value = matchId });

        var count = (long)(await query.ExecuteScalarAsync())!;

        await transaction.CommitAsync();

        return (int)count;
    }

    [Fact]
    public async Task SportfrogPublic_PublishedCompetitionsMatch_WithNoOrganizationContext_IsVisible()
    {
        var seeded = await SeedMatchAsync(competitionIsPublic: true);

        var count = await CountAsPublicAsync(organizationContext: null, seeded.MatchId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task SportfrogPublic_UnpublishedCompetitionsMatch_WithNoOrganizationContext_IsNotVisible()
    {
        var seeded = await SeedMatchAsync(competitionIsPublic: false);

        var count = await CountAsPublicAsync(organizationContext: null, seeded.MatchId);

        count.Should().Be(0);
    }

    /// <summary>
    /// The permissive branch is OR-ed with tenant_isolation, not AND-ed: a
    /// context naming an unrelated organization must not hide a row that is
    /// visible with none, or the landing hero's ticker would go dark for any
    /// visitor who happened to arrive with a stale organization context from
    /// somewhere else in the app.
    /// </summary>
    [Fact]
    public async Task SportfrogPublic_PublishedCompetitionsMatch_WithADifferentOrganizationsContextSet_IsStillVisible()
    {
        var seeded = await SeedMatchAsync(competitionIsPublic: true);

        var count = await CountAsPublicAsync(Guid.NewGuid(), seeded.MatchId);

        count.Should().Be(1);
    }

    /// <summary>
    /// The actual feed the landing hero reads: a finished result under a
    /// freshly seeded, unrelated organization comes back with no
    /// organization-slug restriction to clear and no context established —
    /// exactly how a visitor who has never opened any competition's page
    /// reaches it.
    /// </summary>
    [Fact]
    public async Task ListPublicRecentResults_AsSportfrogPublicWithNoContext_ReturnsTheSeededResult()
    {
        var seeded = await SeedMatchAsync(competitionIsPublic: true, MatchState.Finished, homeTotal: 2, awayTotal: 1);

        await using var connection = new NpgsqlConnection(fixture.PublicConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT home_team_name, away_team_name, home_total, away_total, status
            FROM list_public_recent_results(24)
            """,
            connection);

        // Leido como texto, no como MatchState: el mapeo de enums de Npgsql
        // vive en el NpgsqlDataSource armado por la app (SportFrogDataSource
        // .MapEnums), no en una NpgsqlConnection cruda como esta -- el enum
        // de Postgres se lee igual de bien como su propio texto.
        await using var reader = await command.ExecuteReaderAsync();
        var filas = new List<(string Home, string Away, int? HomeTotal, int? AwayTotal, string Status)>();

        while (await reader.ReadAsync())
        {
            filas.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetString(4)));
        }

        filas.Should().ContainSingle(fila =>
            fila.Home == seeded.HomeTeamName
            && fila.Away == seeded.AwayTeamName
            && fila.HomeTotal == 2
            && fila.AwayTotal == 1
            && fila.Status == "finished");
    }

    [Fact]
    public async Task ListPublicRecentResults_UnpublishedCompetitionsMatch_IsNotReturned()
    {
        var seeded = await SeedMatchAsync(competitionIsPublic: false, MatchState.Finished, homeTotal: 2, awayTotal: 1);

        await using var connection = new NpgsqlConnection(fixture.PublicConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT home_team_name FROM list_public_recent_results(24)", connection);

        await using var reader = await command.ExecuteReaderAsync();
        var nombres = new List<string>();

        while (await reader.ReadAsync())
        {
            nombres.Add(reader.GetString(0));
        }

        nombres.Should().NotContain(seeded.HomeTeamName);
    }
}

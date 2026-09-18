using AwesomeAssertions;
using SportFrog.Api.Features.Standings;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Standings;

/// <summary>
/// A knockout-only category has no group stage to tabulate: every match is a
/// single-elimination tie, not a round of a table. <see cref="StandingsQuery"/>
/// must refuse to fold those into a standings result instead of quietly
/// computing one that counts a bracket win the same as a league result — the
/// bug this guards is the query silently running anyway rather than a
/// caller having to remember to gate it.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class StandingsQueryTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(Guid OrgId, Guid CategoryId);

    private async Task<Fixture> SeedAsync(string format)
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
            Format = format,
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Primera" });
        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = categoryId, Name = "Equipo B" });
        await setup.SaveChangesAsync();

        // Phase set only for the knockout case -- a league round never
        // carries one, and it is Format alone the query must key off, not
        // whether any given match happens to have a phase.
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
            Phase = format == CompetitionFormat.Knockout ? "final" : null,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, categoryId);
    }

    [Fact]
    public async Task ForCategoryAsync_ReturnsNull_ForAKnockoutOnlyCategory()
    {
        var fx = await SeedAsync(CompetitionFormat.Knockout);
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await StandingsQuery.ForCategoryAsync(context, fx.CategoryId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ForCategoryAsync_StillBuildsATable_ForALeagueCategory()
    {
        var fx = await SeedAsync(CompetitionFormat.League);
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await StandingsQuery.ForCategoryAsync(context, fx.CategoryId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Groups.Should().ContainSingle();
        result.Groups[0].Rows.Should().Contain(row => row.Points == 3);
    }
}

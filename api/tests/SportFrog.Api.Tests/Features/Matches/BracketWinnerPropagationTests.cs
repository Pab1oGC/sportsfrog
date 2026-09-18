using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Matches;

/// <summary>
/// Whether a decided match correctly fills in the later match waiting on its
/// winner — the mechanism that lets a knockout be drawn all the way to the
/// final before it is known who reaches it (<c>Bracket.FullDraw</c>) still
/// resolve itself round by round, without <c>AdvanceBracket</c> ever
/// drawing anything new.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class BracketWinnerPropagationTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(
        Guid OrgId, Guid CompetitionId, Guid CategoryId, Guid UserId,
        Guid TeamAId, Guid TeamBId, Guid TeamCId, Guid TeamDId);

    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var clubCId = Guid.NewGuid();
        var clubDId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        var teamCId = Guid.NewGuid();
        var teamDId = Guid.NewGuid();

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
        setup.Clubs.Add(new Club { Id = clubCId, OrgId = orgId, Name = "Club C" });
        setup.Clubs.Add(new Club { Id = clubDId, OrgId = orgId, Name = "Club D" });

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
            Format = CompetitionFormat.Knockout,
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Categoria" });

        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = categoryId, Name = "Equipo B" });
        setup.Teams.Add(new Team { Id = teamCId, OrgId = orgId, ClubId = clubCId, CategoryId = categoryId, Name = "Equipo C" });
        setup.Teams.Add(new Team { Id = teamDId, OrgId = orgId, ClubId = clubDId, CategoryId = categoryId, Name = "Equipo D" });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, competitionId, categoryId, userId, teamAId, teamBId, teamCId, teamDId);
    }

    [Fact]
    public async Task ApplyAsync_SourceMatchDecided_FillsTheDependentsWaitingSide()
    {
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        // Semifinal 1: A vs B, already finished 2-1. The final is waiting on
        // its winner for the home side, and on an unrelated semifinal (not
        // seeded here) for the away side — exactly the shape Bracket.FullDraw
        // produces, minus the second semifinal this test does not need.
        var semi1 = new Match
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            CategoryId = fx.CategoryId,
            HomeTeamId = fx.TeamAId,
            AwayTeamId = fx.TeamBId,
            HomeTotal = 2,
            AwayTotal = 1,
            RoundNumber = 1,
            Phase = "semifinal",
            Status = MatchState.Finished,
            RecordedBy = fx.UserId,
        };

        var final = new Match
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            CategoryId = fx.CategoryId,
            HomeTeamId = null,
            AwayTeamId = fx.TeamDId,
            HomeSourceMatchId = semi1.Id,
            AwaySourceMatchId = null,
            RoundNumber = 2,
            Phase = "final",
            Status = MatchState.Scheduled,
        };

        context.Matches.AddRange(semi1, final);
        await context.SaveChangesAsync();

        await BracketWinnerPropagation.ApplyAsync(semi1, context, CancellationToken.None);
        await context.SaveChangesAsync();

        var reread = await context.Matches
            .AsNoTracking()
            .SingleAsync(match => match.Id == final.Id, CancellationToken.None);

        reread.HomeTeamId.Should().Be(fx.TeamAId, "team A won semifinal 1 two goals to one");
        reread.HasBothTeams.Should().BeTrue();

        // The source stays — it is also how the bracket draws its own lines,
        // not cleared once it has done its job.
        reread.HomeSourceMatchId.Should().Be(semi1.Id);
    }

    [Fact]
    public async Task ApplyAsync_SourceMatchStillLevelWithNoShootout_LeavesTheDependentUnresolved()
    {
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var semi1 = new Match
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            CategoryId = fx.CategoryId,
            HomeTeamId = fx.TeamAId,
            AwayTeamId = fx.TeamBId,
            HomeTotal = 1,
            AwayTotal = 1,
            RoundNumber = 1,
            Phase = "semifinal",
            Status = MatchState.Finished,
            RecordedBy = fx.UserId,
        };

        var final = new Match
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            CategoryId = fx.CategoryId,
            HomeTeamId = null,
            AwayTeamId = fx.TeamDId,
            HomeSourceMatchId = semi1.Id,
            RoundNumber = 2,
            Phase = "final",
            Status = MatchState.Scheduled,
        };

        context.Matches.AddRange(semi1, final);
        await context.SaveChangesAsync();

        await BracketWinnerPropagation.ApplyAsync(semi1, context, CancellationToken.None);
        await context.SaveChangesAsync();

        var reread = await context.Matches
            .AsNoTracking()
            .SingleAsync(match => match.Id == final.Id, CancellationToken.None);

        // A level result with no shootout recorded has not actually decided
        // the tie — nobody to fill the final's side with yet.
        reread.HomeTeamId.Should().BeNull();
        reread.HasBothTeams.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_MatchWithNoDependents_DoesNothing()
    {
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var lone = new Match
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            CategoryId = fx.CategoryId,
            HomeTeamId = fx.TeamAId,
            AwayTeamId = fx.TeamBId,
            HomeTotal = 2,
            AwayTotal = 0,
            RoundNumber = 1,
            Phase = "final",
            Status = MatchState.Finished,
            RecordedBy = fx.UserId,
        };

        context.Matches.Add(lone);
        await context.SaveChangesAsync();

        var act = async () =>
        {
            await BracketWinnerPropagation.ApplyAsync(lone, context, CancellationToken.None);
            await context.SaveChangesAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ApplyAsync_NonBracketMatch_NeverLooksForDependents()
    {
        // Phase null is a league match — nothing Bracket.FullDraw ever draws
        // carries that, so this is refused before the query that would find
        // dependents even runs.
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var leagueMatch = new Match
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            CategoryId = fx.CategoryId,
            HomeTeamId = fx.TeamAId,
            AwayTeamId = fx.TeamBId,
            HomeTotal = 1,
            AwayTotal = 0,
            RoundNumber = 1,
            Phase = null,
            Status = MatchState.Finished,
            RecordedBy = fx.UserId,
        };

        context.Matches.Add(leagueMatch);
        await context.SaveChangesAsync();

        var act = async () => await BracketWinnerPropagation.ApplyAsync(leagueMatch, context, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}

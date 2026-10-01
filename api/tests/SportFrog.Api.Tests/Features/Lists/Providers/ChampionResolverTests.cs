using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>
/// <see cref="ChampionResolver"/> generalizes
/// <c>Public.ReadPublicCompetition.ResolveChampionAsync</c> — these pin that
/// the generalization agrees with it on every format that one already
/// covers (league, groups never promoted, a decided knockout final,
/// walkover), and covers honestly the cases it never had to: a second
/// position, a judged category, and an undecided result that says why
/// instead of reading as "not found".
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ChampionResolverTests(SportFrogDatabaseFixture fixture)
{
    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>Org, user, one club, a ruleset and a competition+category for <paramref name="sportCode"/> under
    /// <paramref name="format"/> — everything every scenario below needs, with its own transaction left open so
    /// the caller can add whatever that scenario needs before committing.</summary>
    private async Task<(SportFrogDbContext Setup, IDbContextTransaction Transaction, Guid OrgId, Guid UserId, Guid ClubId, Guid CompetitionId, Guid CategoryId)>
        OpenCategoryAsync(string sportCode, string format)
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Test User",
        });
        await setup.SaveChangesAsync();

        var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club A" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = sportCode,
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
            SportCode = sportCode,
            RulesetId = rulesetId,
            Name = "Copa de Prueba",
            Slug = $"copa-{competitionId:N}",
            Season = "2026",
            Format = format,
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Primera" });

        return (setup, transaction, orgId, userId, clubId, competitionId, categoryId);
    }

    private async Task<Session> OpenAsync(Guid orgId)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        return new Session(context, transaction);
    }

    private static Team NewTeam(Guid orgId, Guid clubId, Guid categoryId, string name) =>
        new() { Id = Guid.NewGuid(), OrgId = orgId, ClubId = clubId, CategoryId = categoryId, Name = name };

    [Fact]
    public async Task ResolveAsync_UnknownCategory_IsCategoryNotFound()
    {
        var (setup, transaction, orgId, _, _, _, _) = await OpenCategoryAsync("football", CompetitionFormat.League);
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var result = await ChampionResolver.ResolveAsync(session.Context, Guid.NewGuid(), 1, CancellationToken.None);

        result.Resolution.Should().Be(ChampionResolution.CategoryNotFound);
    }

    // ---- League --------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_LeagueDecided_Position1And2_AreWinnerAndLoser()
    {
        var (setup, transaction, orgId, userId, clubId, competitionId, categoryId) = await OpenCategoryAsync("football", CompetitionFormat.League);

        var clubBId = Guid.NewGuid();
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });

        var teamA = NewTeam(orgId, clubId, categoryId, "Equipo A");
        var teamB = NewTeam(orgId, clubBId, categoryId, "Equipo B");
        setup.Teams.AddRange(teamA, teamB);
        await setup.SaveChangesAsync();

        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = teamA.Id, AwayTeamId = teamB.Id, HomeTotal = 2, AwayTotal = 1,
            Status = MatchState.Finished, RecordedBy = userId,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var first = await ChampionResolver.ResolveAsync(session.Context, categoryId, 1, CancellationToken.None);
        var second = await ChampionResolver.ResolveAsync(session.Context, categoryId, 2, CancellationToken.None);

        first.Resolution.Should().Be(ChampionResolution.Resolved);
        first.TeamId.Should().Be(teamA.Id);
        second.TeamId.Should().Be(teamB.Id);
    }

    [Fact]
    public async Task ResolveAsync_LeaguePastItsLastTeam_IsUndecided()
    {
        var (setup, transaction, orgId, userId, clubId, competitionId, categoryId) = await OpenCategoryAsync("football", CompetitionFormat.League);

        var clubBId = Guid.NewGuid();
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });

        var teamA = NewTeam(orgId, clubId, categoryId, "Equipo A");
        var teamB = NewTeam(orgId, clubBId, categoryId, "Equipo B");
        setup.Teams.AddRange(teamA, teamB);
        await setup.SaveChangesAsync();

        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = teamA.Id, AwayTeamId = teamB.Id, HomeTotal = 2, AwayTotal = 1,
            Status = MatchState.Finished, RecordedBy = userId,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var result = await ChampionResolver.ResolveAsync(session.Context, categoryId, 3, CancellationToken.None);

        result.Resolution.Should().Be(ChampionResolution.Undecided);
        result.Reason.Should().Be("Esta categoría no tiene tantos equipos.");
    }

    [Fact]
    public async Task ResolveAsync_LeagueWithNoMatchesPlayed_IsUndecided()
    {
        var (setup, transaction, orgId, _, clubId, competitionId, categoryId) = await OpenCategoryAsync("football", CompetitionFormat.League);

        var clubBId = Guid.NewGuid();
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });
        setup.Teams.AddRange(
            NewTeam(orgId, clubId, categoryId, "Equipo A"), NewTeam(orgId, clubBId, categoryId, "Equipo B"));
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var result = await ChampionResolver.ResolveAsync(session.Context, categoryId, 1, CancellationToken.None);

        result.Resolution.Should().Be(ChampionResolution.Undecided);
        result.Reason.Should().Be("Todavía no hay resultados cargados en esta categoría.");
    }

    // ---- Groups, never promoted -----------------------------------------

    [Fact]
    public async Task ResolveAsync_TwoGroupsNeverPromotedToAKnockout_IsUndecided()
    {
        var (setup, transaction, orgId, userId, clubId, competitionId, categoryId) = await OpenCategoryAsync("football", CompetitionFormat.Groups);

        var clubBId = Guid.NewGuid();
        var clubCId = Guid.NewGuid();
        var clubDId = Guid.NewGuid();
        setup.Clubs.AddRange(
            new Club { Id = clubBId, OrgId = orgId, Name = "Club B" },
            new Club { Id = clubCId, OrgId = orgId, Name = "Club C" },
            new Club { Id = clubDId, OrgId = orgId, Name = "Club D" });

        var a = NewTeam(orgId, clubId, categoryId, "Equipo A");
        a.GroupLabel = "A";
        var b = NewTeam(orgId, clubBId, categoryId, "Equipo B");
        b.GroupLabel = "A";
        var c = NewTeam(orgId, clubCId, categoryId, "Equipo C");
        c.GroupLabel = "B";
        var d = NewTeam(orgId, clubDId, categoryId, "Equipo D");
        d.GroupLabel = "B";
        setup.Teams.AddRange(a, b, c, d);
        await setup.SaveChangesAsync();

        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = a.Id, AwayTeamId = b.Id, HomeTotal = 2, AwayTotal = 1,
            Status = MatchState.Finished, RecordedBy = userId,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });
        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = c.Id, AwayTeamId = d.Id, HomeTotal = 1, AwayTotal = 0,
            Status = MatchState.Finished, RecordedBy = userId,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 17, 0, 0, TimeSpan.Zero),
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var result = await ChampionResolver.ResolveAsync(session.Context, categoryId, 1, CancellationToken.None);

        result.Resolution.Should().Be(ChampionResolution.Undecided);
        result.Reason.Should().Be("Esta categoría tiene más de un grupo, y ninguno por sí solo define este puesto.");
    }

    // ---- Knockout --------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_KnockoutFinalDecided_Position1And2_AreWinnerAndLoser_Position3_IsUndecided()
    {
        var (setup, transaction, orgId, userId, clubId, competitionId, categoryId) = await OpenCategoryAsync("football", CompetitionFormat.Knockout);

        var clubBId = Guid.NewGuid();
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });

        var teamA = NewTeam(orgId, clubId, categoryId, "Equipo A");
        var teamB = NewTeam(orgId, clubBId, categoryId, "Equipo B");
        setup.Teams.AddRange(teamA, teamB);
        await setup.SaveChangesAsync();

        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = teamA.Id, AwayTeamId = teamB.Id, HomeTotal = 3, AwayTotal = 1,
            Status = MatchState.Finished, RecordedBy = userId, Phase = Bracket.FinalPhase, RoundNumber = 1,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var first = await ChampionResolver.ResolveAsync(session.Context, categoryId, 1, CancellationToken.None);
        var second = await ChampionResolver.ResolveAsync(session.Context, categoryId, 2, CancellationToken.None);
        var third = await ChampionResolver.ResolveAsync(session.Context, categoryId, 3, CancellationToken.None);

        first.TeamId.Should().Be(teamA.Id);
        second.TeamId.Should().Be(teamB.Id);
        third.Resolution.Should().Be(ChampionResolution.Undecided);
        third.Reason.Should().Be("Esta categoría no define un tercer puesto a partir de la llave.");
    }

    [Fact]
    public async Task ResolveAsync_KnockoutFinalDecidedByWalkover_ReadsTheAwardedWinner()
    {
        var (setup, transaction, orgId, userId, clubId, competitionId, categoryId) = await OpenCategoryAsync("football", CompetitionFormat.Knockout);

        var clubBId = Guid.NewGuid();
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });

        var teamA = NewTeam(orgId, clubId, categoryId, "Equipo A");
        var teamB = NewTeam(orgId, clubBId, categoryId, "Equipo B");
        setup.Teams.AddRange(teamA, teamB);
        await setup.SaveChangesAsync();

        // Shaped the way AwardWalkover itself leaves a match: a real score
        // difference favouring the winner, status Walkover rather than
        // Finished, and the award recorded on WalkoverTeamId too.
        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = teamA.Id, AwayTeamId = teamB.Id, HomeTotal = 3, AwayTotal = 0,
            WalkoverTeamId = teamA.Id, Status = MatchState.Walkover, RecordedBy = userId,
            Phase = Bracket.FinalPhase, RoundNumber = 1,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var result = await ChampionResolver.ResolveAsync(session.Context, categoryId, 1, CancellationToken.None);

        result.Resolution.Should().Be(ChampionResolution.Resolved);
        result.TeamId.Should().Be(teamA.Id);
    }

    [Fact]
    public async Task ResolveAsync_KnockoutFinalNotPlayedYet_IsUndecided()
    {
        var (setup, transaction, orgId, _, clubId, competitionId, categoryId) = await OpenCategoryAsync("football", CompetitionFormat.Knockout);

        var clubBId = Guid.NewGuid();
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });

        var teamA = NewTeam(orgId, clubId, categoryId, "Equipo A");
        var teamB = NewTeam(orgId, clubBId, categoryId, "Equipo B");
        setup.Teams.AddRange(teamA, teamB);
        await setup.SaveChangesAsync();

        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = teamA.Id, AwayTeamId = teamB.Id, Status = MatchState.Scheduled,
            Phase = Bracket.FinalPhase, RoundNumber = 1,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var result = await ChampionResolver.ResolveAsync(session.Context, categoryId, 1, CancellationToken.None);

        result.Resolution.Should().Be(ChampionResolution.Undecided);
        result.Reason.Should().Be("La final de esta categoría todavía no se jugó.");
    }

    [Fact]
    public async Task ResolveAsync_KnockoutFinalSlotsNotYetFilledBySemifinals_IsUndecided()
    {
        var (setup, transaction, orgId, _, clubId, competitionId, categoryId) = await OpenCategoryAsync("football", CompetitionFormat.Knockout);

        var clubBId = Guid.NewGuid();
        var clubCId = Guid.NewGuid();
        var clubDId = Guid.NewGuid();
        setup.Clubs.AddRange(
            new Club { Id = clubBId, OrgId = orgId, Name = "Club B" },
            new Club { Id = clubCId, OrgId = orgId, Name = "Club C" },
            new Club { Id = clubDId, OrgId = orgId, Name = "Club D" });

        var teamA = NewTeam(orgId, clubId, categoryId, "Equipo A");
        var teamB = NewTeam(orgId, clubBId, categoryId, "Equipo B");
        var teamC = NewTeam(orgId, clubCId, categoryId, "Equipo C");
        var teamD = NewTeam(orgId, clubDId, categoryId, "Equipo D");
        setup.Teams.AddRange(teamA, teamB, teamC, teamD);
        await setup.SaveChangesAsync();

        var semifinal1Id = Guid.NewGuid();
        var semifinal2Id = Guid.NewGuid();

        setup.Matches.Add(new Match
        {
            Id = semifinal1Id, OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = teamA.Id, AwayTeamId = teamB.Id, Status = MatchState.Scheduled,
            Phase = "semifinal", RoundNumber = 1, ScheduledAt = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
        });
        setup.Matches.Add(new Match
        {
            Id = semifinal2Id, OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = teamC.Id, AwayTeamId = teamD.Id, Status = MatchState.Scheduled,
            Phase = "semifinal", RoundNumber = 1, ScheduledAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
        });
        await setup.SaveChangesAsync();

        // Drawn in full: the final's two slots point at the semifinals that
        // will fill them, rather than naming a team — exactly the shape
        // AddBracketPlaceholderSlots exists for.
        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeSourceMatchId = semifinal1Id, AwaySourceMatchId = semifinal2Id, Status = MatchState.Scheduled,
            Phase = Bracket.FinalPhase, RoundNumber = 2, ScheduledAt = new DateTimeOffset(2026, 5, 2, 15, 0, 0, TimeSpan.Zero),
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var result = await ChampionResolver.ResolveAsync(session.Context, categoryId, 1, CancellationToken.None);

        result.Resolution.Should().Be(ChampionResolution.Undecided);
        result.Reason.Should().Be("La final de esta categoría todavía no está definida.");
    }

    // ---- Judged ------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_JudgedCategoryWithScores_Position1And2_AreHighestAndSecondScore()
    {
        var (setup, transaction, orgId, _, clubId, competitionId, categoryId) = await OpenCategoryAsync("taekwondo_poomsae", CompetitionFormat.League);

        var clubBId = Guid.NewGuid();
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });

        var teamA = NewTeam(orgId, clubId, categoryId, "Atleta A");
        var teamB = NewTeam(orgId, clubBId, categoryId, "Atleta B");
        setup.Teams.AddRange(teamA, teamB);
        await setup.SaveChangesAsync();

        setup.Performances.Add(new Performance
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            TeamId = teamA.Id, Score = 85,
        });
        setup.Performances.Add(new Performance
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            TeamId = teamB.Id, Score = 70,
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var first = await ChampionResolver.ResolveAsync(session.Context, categoryId, 1, CancellationToken.None);
        var second = await ChampionResolver.ResolveAsync(session.Context, categoryId, 2, CancellationToken.None);

        first.Resolution.Should().Be(ChampionResolution.Resolved);
        first.TeamId.Should().Be(teamA.Id);
        second.TeamId.Should().Be(teamB.Id);
    }

    [Fact]
    public async Task ResolveAsync_JudgedCategoryWithNoScoresYet_IsUndecided()
    {
        var (setup, transaction, orgId, _, clubId, competitionId, categoryId) = await OpenCategoryAsync("taekwondo_poomsae", CompetitionFormat.League);

        var teamA = NewTeam(orgId, clubId, categoryId, "Atleta A");
        setup.Teams.Add(teamA);
        await setup.SaveChangesAsync();

        setup.Performances.Add(new Performance
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            TeamId = teamA.Id, Score = null,
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        await using var session = await OpenAsync(orgId);

        var result = await ChampionResolver.ResolveAsync(session.Context, categoryId, 1, CancellationToken.None);

        result.Resolution.Should().Be(ChampionResolution.Undecided);
        result.Reason.Should().Be("Todavía no hay resultados cargados en esta categoría.");
    }
}

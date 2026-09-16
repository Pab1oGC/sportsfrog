using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Draw;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Draw;

/// <summary>
/// The two DB-backed decisions <see cref="ScheduleCalendar"/> makes before it
/// ever calls <c>CalendarPlacement</c>: which jornada is next, and how long
/// one of its matches takes. The placement algorithm itself is exercised
/// without a database in <c>CalendarPlacementTests</c>; this is the query and
/// resolution logic in front of it, which needs a real schema to mean
/// anything (ordering by a real column, joining a real ruleset).
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ScheduleCalendarTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Seeded(
        Guid OrgId,
        Guid CompetitionId,
        Guid EarlyCategoryId,
        Guid LateCategoryId,
        Guid TimedRulesetId,
        Guid ClocklessRulesetId);

    /// <summary>
    /// Two categories in one competition: "Early" (DisplayOrder 1, football
    /// rules) and "Late" (DisplayOrder 2, the same shape) — the block order
    /// this whole feature exists to respect. Each gets two teams and two
    /// rounds of one match apiece, all still unscheduled.
    /// </summary>
    private async Task<Seeded> SeedAsync(short? clocklessEstimatedMinutes = null, short? bufferMinutes = null)
    {
        var orgId = Guid.NewGuid();
        var clubOneId = Guid.NewGuid();
        var clubTwoId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var timedRulesetId = Guid.NewGuid();
        var clocklessRulesetId = Guid.NewGuid();
        var earlyCategoryId = Guid.NewGuid();
        var lateCategoryId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubOneId, OrgId = orgId, Name = "Club Uno" });
        setup.Clubs.Add(new Club { Id = clubTwoId, OrgId = orgId, Name = "Club Dos" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = timedRulesetId,
            OrgId = orgId,
            SportCode = "football",
            Name = "Fútbol con reloj",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45, BreakMinutes = 15 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Rulesets.Add(new Ruleset
        {
            Id = clocklessRulesetId,
            OrgId = orgId,
            SportCode = "wally",
            Name = "Wally sin reloj",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 3, Label = "set", Minutes = null, EstimatedMinutes = clocklessEstimatedMinutes },
                Points = new Dictionary<string, int>(),
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId,
            OrgId = orgId,
            SportCode = "football",
            RulesetId = timedRulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings
            {
                Schedule = new ScheduleSettings { BufferMinutes = bufferMinutes },
            },
        });

        setup.Categories.Add(new Category
        {
            Id = earlyCategoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Early", DisplayOrder = 1,
        });
        setup.Categories.Add(new Category
        {
            Id = lateCategoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Late", DisplayOrder = 2,
        });

        // Two clubs, each fielding one team per category — uq_teams_club_category
        // allows a club exactly one team per category, so re-using the same
        // club for both teams of one category would collide.
        var teams = new (Guid Id, Guid CategoryId, Guid ClubId)[]
        {
            (Guid.NewGuid(), earlyCategoryId, clubOneId), (Guid.NewGuid(), earlyCategoryId, clubTwoId),
            (Guid.NewGuid(), lateCategoryId, clubOneId), (Guid.NewGuid(), lateCategoryId, clubTwoId),
        };

        foreach (var (id, categoryId, teamClubId) in teams)
        {
            setup.Teams.Add(new Team { Id = id, OrgId = orgId, ClubId = teamClubId, CategoryId = categoryId, Name = $"Equipo {id:N}" });
        }

        void AddMatch(Guid categoryId, Guid home, Guid away, short round) =>
            setup.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                CompetitionId = competitionId,
                CategoryId = categoryId,
                HomeTeamId = home,
                AwayTeamId = away,
                RoundNumber = round,
                Status = MatchState.Scheduled,
            });

        AddMatch(earlyCategoryId, teams[0].Id, teams[1].Id, round: 1);
        AddMatch(earlyCategoryId, teams[1].Id, teams[0].Id, round: 2);
        AddMatch(lateCategoryId, teams[2].Id, teams[3].Id, round: 1);
        AddMatch(lateCategoryId, teams[3].Id, teams[2].Id, round: 2);

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Seeded(orgId, competitionId, earlyCategoryId, lateCategoryId, timedRulesetId, clocklessRulesetId);
    }

    // ---- FindNextJornadaAsync -------------------------------------------

    [Fact]
    public async Task FindNextJornada_TwoCategoriesBothPending_PicksTheLowerDisplayOrder()
    {
        var seeded = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var orgId = seeded.OrgId;
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var next = await ScheduleCalendar.FindNextJornadaAsync(context, seeded.CompetitionId, CancellationToken.None);

        next.Should().NotBeNull();
        next!.CategoryId.Should().Be(seeded.EarlyCategoryId);
        next.CategoryName.Should().Be("Early");
        next.RoundNumber.Should().Be((short)1);
    }

    [Fact]
    public async Task FindNextJornada_EarlyCategoryFullyScheduled_MovesToTheNextCategory()
    {
        var seeded = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var orgId = seeded.OrgId;
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        // Every "Early" match already has a date — nothing left for that
        // category to owe.
        await context.Matches
            .Where(match => match.CategoryId == seeded.EarlyCategoryId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(match => match.ScheduledAt, DateTimeOffset.UtcNow));

        var next = await ScheduleCalendar.FindNextJornadaAsync(context, seeded.CompetitionId, CancellationToken.None);

        next.Should().NotBeNull();
        next!.CategoryId.Should().Be(seeded.LateCategoryId);
        next.RoundNumber.Should().Be((short)1);
    }

    [Fact]
    public async Task FindNextJornada_EarlyCategoryRoundOneDone_StaysOnEarlyCategoryButMovesToRoundTwo()
    {
        var seeded = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var orgId = seeded.OrgId;
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        await context.Matches
            .Where(match => match.CategoryId == seeded.EarlyCategoryId && match.RoundNumber == 1)
            .ExecuteUpdateAsync(update => update
                .SetProperty(match => match.ScheduledAt, DateTimeOffset.UtcNow));

        var next = await ScheduleCalendar.FindNextJornadaAsync(context, seeded.CompetitionId, CancellationToken.None);

        next!.CategoryId.Should().Be(seeded.EarlyCategoryId);
        next.RoundNumber.Should().Be((short)2);
    }

    [Fact]
    public async Task FindNextJornada_NothingPending_IsNull()
    {
        var seeded = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var orgId = seeded.OrgId;
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        await context.Matches
            .Where(match => match.CompetitionId == seeded.CompetitionId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(match => match.ScheduledAt, DateTimeOffset.UtcNow));

        var next = await ScheduleCalendar.FindNextJornadaAsync(context, seeded.CompetitionId, CancellationToken.None);

        next.Should().BeNull();
    }

    // ---- ResolveDurationsAsync -------------------------------------------

    [Fact]
    public async Task ResolveDurations_ClockedSport_ComputesFromTheRulesetPlusDefaultBuffer()
    {
        var seeded = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var orgId = seeded.OrgId;
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var resolved = await ScheduleCalendar.ResolveDurationsAsync(
            context, [seeded.EarlyCategoryId], CancellationToken.None);

        resolved[seeded.EarlyCategoryId].Should().Be(
            new ScheduleCalendar.ResolvedDuration(105, ScheduleCalendar.DefaultBufferMinutes)); // 45+45+15
    }

    [Fact]
    public async Task ResolveDurations_ClockedSportWithACustomBuffer_UsesItInsteadOfTheDefault()
    {
        var seeded = await SeedAsync(bufferMinutes: 5);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var orgId = seeded.OrgId;
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var resolved = await ScheduleCalendar.ResolveDurationsAsync(
            context, [seeded.EarlyCategoryId], CancellationToken.None);

        resolved[seeded.EarlyCategoryId].Should().Be(new ScheduleCalendar.ResolvedDuration(105, 5));
    }

    [Fact]
    public async Task ResolveDurations_ClocklessSportWithNoEstimateDeclared_CannotBeResolved()
    {
        var seeded = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var orgId = seeded.OrgId;
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        // Override the Early category onto the clockless ruleset directly.
        await context.Categories
            .Where(category => category.Id == seeded.EarlyCategoryId)
            .ExecuteUpdateAsync(update => update.SetProperty(category => category.RulesetId, seeded.ClocklessRulesetId));

        var resolved = await ScheduleCalendar.ResolveDurationsAsync(
            context, [seeded.EarlyCategoryId], CancellationToken.None);

        resolved[seeded.EarlyCategoryId].Should().BeNull();
    }

    [Fact]
    public async Task ResolveDurations_ClocklessSportWithAnEstimateDeclared_UsesItPlusDefaultBuffer()
    {
        var seeded = await SeedAsync(clocklessEstimatedMinutes: 40);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var orgId = seeded.OrgId;
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        await context.Categories
            .Where(category => category.Id == seeded.EarlyCategoryId)
            .ExecuteUpdateAsync(update => update.SetProperty(category => category.RulesetId, seeded.ClocklessRulesetId));

        var resolved = await ScheduleCalendar.ResolveDurationsAsync(
            context, [seeded.EarlyCategoryId], CancellationToken.None);

        // Buffer applies exactly the same way it does for a clocked sport —
        // duration and buffer are cleanly separate concerns regardless of
        // where the duration itself came from.
        resolved[seeded.EarlyCategoryId].Should().Be(
            new ScheduleCalendar.ResolvedDuration(40, ScheduleCalendar.DefaultBufferMinutes));
    }

    [Fact]
    public async Task ResolveDurations_TwoCategoriesAtOnce_ResolvesEachIndependently()
    {
        var seeded = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var orgId = seeded.OrgId;
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        await context.Categories
            .Where(category => category.Id == seeded.LateCategoryId)
            .ExecuteUpdateAsync(update => update.SetProperty(category => category.RulesetId, seeded.ClocklessRulesetId));

        var resolved = await ScheduleCalendar.ResolveDurationsAsync(
            context, [seeded.EarlyCategoryId, seeded.LateCategoryId], CancellationToken.None);

        resolved[seeded.EarlyCategoryId].Should().Be(new ScheduleCalendar.ResolvedDuration(105, ScheduleCalendar.DefaultBufferMinutes));
        resolved[seeded.LateCategoryId].Should().BeNull(); // clockless, and its reglamento never declared an estimate
    }
}

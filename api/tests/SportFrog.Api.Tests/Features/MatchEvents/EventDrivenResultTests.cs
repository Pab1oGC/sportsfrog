using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Discipline;
using SportFrog.Api.Features.MatchEvents;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.MatchEvents;

/// <summary>
/// Whether a volleyball match's own score keeps agreeing with its events —
/// at the moment a result is typed in by hand
/// (<see cref="RecordResult.HandleAsync"/>'s own cross-check), and from then
/// on, every time one of those events is recorded, corrected or deleted
/// (<see cref="EventDrivenResult"/>). Volleyball rather than football:
/// AddVolleyballScoringMetrics is what made this a question events can
/// answer for a sets-mode sport at all, and it is the sport this whole
/// phase was written for.
///
/// Exercises <see cref="EventDrivenResult"/> directly against rows written
/// straight to <see cref="SportFrogDbContext.PlayerEvents"/>, the same
/// state <see cref="RecordEvent"/>, <see cref="CorrectEvent"/> and
/// <see cref="DeleteEvent"/> leave behind right before each one calls it —
/// EventPeriodTests already covers whether those three accept or refuse a
/// given event; this is about what happens to the match once one lands.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class EventDrivenResultTests(SportFrogDatabaseFixture fixture)
{
    private const string Volleyball = "volleyball";
    private const string Football = "football";
    private const string Kyorugi = "taekwondo_kyorugi";

    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // The real registries, the same wiring Program.cs assembles through the
    // container — see RecordResultSanctionsTests' own remark on why this is
    // not mocked.
    private static readonly IMatchOutcomeRulesRegistry OutcomeRules =
        new MatchOutcomeRulesRegistry(
            [new CumulativeMatchOutcomeRules(), new SetsMatchOutcomeRules(), new JudgedMatchOutcomeRules()]);

    private static readonly ResultPolicy Policy = new(
        new ResultShapeRulesRegistry(
            [new CumulativeResultShape(), new SetsResultShape(OutcomeRules), new JudgedResultShape()]));

    private sealed record Fixture(
        Guid OrgId, Guid UserId, Guid MatchId, Guid HomeTeamId, Guid AwayTeamId,
        Guid HomeEntryId, Guid AwayEntryId, Guid PointMetricId, Guid CategoryId, Guid CompetitionId);

    private static OrganizationContext OrganizationFor(Guid orgId, Guid userId)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Recorder, userId, ipAddress: null);
        return organization;
    }

    private static EventDrivenResult ResyncFor(SportFrogDbContext context, Guid orgId, Guid userId) =>
        new(new MatchRulesLookup(context), Policy, OutcomeRules, OrganizationFor(orgId, userId), Clock);

    /// <summary>
    /// One organization, one volleyball competition captured in detail, two
    /// teams with one registered player each, and one match already in
    /// progress — enough to record points for real and finish it either way
    /// RecordResult offers.
    /// </summary>
    private async Task<Fixture> SeedAsync(CaptureLevel captureLevel = CaptureLevel.Detailed)
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var homeClubId = Guid.NewGuid();
        var awayClubId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var homeAthleteId = Guid.NewGuid();
        var awayAthleteId = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Planillero",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = homeClubId, OrgId = orgId, Name = "Club Local" });
        setup.Clubs.Add(new Club { Id = awayClubId, OrgId = orgId, Name = "Club Visitante" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = Volleyball,
            Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 5, Label = "set", Minutes = null, EstimatedMinutes = 25 },
                Points = new Dictionary<string, int>
                {
                    ["win_3_0"] = 3, ["loss_0_3"] = 0,
                    ["win_3_1"] = 3, ["loss_1_3"] = 0,
                    ["win_3_2"] = 3, ["loss_2_3"] = 1,
                },
                Tiebreakers = ["score_difference"],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId,
            OrgId = orgId,
            SportCode = Volleyball,
            RulesetId = rulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "knockout",
            CaptureLevel = captureLevel,
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Categoria" });
        setup.Teams.Add(new Team { Id = homeTeamId, OrgId = orgId, ClubId = homeClubId, CategoryId = categoryId, Name = "Equipo Local" });
        setup.Teams.Add(new Team { Id = awayTeamId, OrgId = orgId, ClubId = awayClubId, CategoryId = categoryId, Name = "Equipo Visitante" });

        setup.Athletes.Add(new Athlete
        {
            Id = homeAthleteId, OrgId = orgId, FirstName = "Local", LastName = "DePrueba",
            DocumentId = $"doc-{homeAthleteId:N}", BirthDate = new DateOnly(2000, 1, 1),
        });
        setup.Athletes.Add(new Athlete
        {
            Id = awayAthleteId, OrgId = orgId, FirstName = "Visitante", LastName = "DePrueba",
            DocumentId = $"doc-{awayAthleteId:N}", BirthDate = new DateOnly(2000, 1, 1),
        });

        var homeEntryId = Guid.NewGuid();
        var awayEntryId = Guid.NewGuid();
        setup.RosterEntries.Add(new RosterEntry { Id = homeEntryId, OrgId = orgId, TeamId = homeTeamId, AthleteId = homeAthleteId });
        setup.RosterEntries.Add(new RosterEntry { Id = awayEntryId, OrgId = orgId, TeamId = awayTeamId, AthleteId = awayAthleteId });

        setup.Matches.Add(new Match
        {
            Id = matchId,
            OrgId = orgId,
            CompetitionId = competitionId,
            CategoryId = categoryId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Phase = "Semifinal",
            Status = MatchState.InProgress,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        var pointMetricId = await setup.SportMetrics.AsNoTracking()
            .Where(metric => metric.SportCode == Volleyball && metric.Code == "point")
            .Select(metric => metric.Id).SingleAsync();

        return new Fixture(
            orgId, userId, matchId, homeTeamId, awayTeamId, homeEntryId, awayEntryId, pointMetricId, categoryId,
            competitionId);
    }

    /// <summary>Writes a point directly, then resyncs — the exact state RecordEvent leaves behind.</summary>
    private async Task<Guid> RecordPointAsync(Fixture fx, Guid rosterEntryId, short period)
    {
        var eventId = Guid.NewGuid();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.PlayerEvents.Add(new PlayerEvent
        {
            Id = eventId, OrgId = fx.OrgId, MatchId = fx.MatchId, RosterEntryId = rosterEntryId,
            MetricId = fx.PointMetricId, PeriodNumber = period, Quantity = 1,
        });
        await context.SaveChangesAsync();
        await ResyncFor(context, fx.OrgId, fx.UserId).ResyncAsync(fx.MatchId, context, CancellationToken.None);

        await transaction.CommitAsync();
        return eventId;
    }

    /// <summary>Moves an event to a different period directly, then resyncs — the exact state CorrectEvent leaves behind.</summary>
    private async Task CorrectEventPeriodAsync(Fixture fx, Guid eventId, short period)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var recorded = await context.PlayerEvents.SingleAsync(candidate => candidate.Id == eventId);
        recorded.PeriodNumber = period;
        await context.SaveChangesAsync();
        await ResyncFor(context, fx.OrgId, fx.UserId).ResyncAsync(fx.MatchId, context, CancellationToken.None);

        await transaction.CommitAsync();
    }

    /// <summary>Removes an event directly, then resyncs — the exact state DeleteEvent leaves behind.</summary>
    private async Task DeleteEventAsync(Fixture fx, Guid eventId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var recorded = await context.PlayerEvents.SingleAsync(candidate => candidate.Id == eventId);
        context.PlayerEvents.Remove(recorded);
        await context.SaveChangesAsync();
        await ResyncFor(context, fx.OrgId, fx.UserId).ResyncAsync(fx.MatchId, context, CancellationToken.None);

        await transaction.CommitAsync();
    }

    private async Task<IResult> FinishFromEventsAsync(Fixture fx)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var activeSanctions = new ActiveSanctions(context, new SanctionProgress(context), new SanctionFinePayments(context));

        var result = await RecordResult.HandleFromEventsAsync(
            fx.MatchId, context, new MatchRulesLookup(context), Policy, OutcomeRules, activeSanctions,
            OrganizationFor(fx.OrgId, fx.UserId), Clock, CancellationToken.None);

        await transaction.CommitAsync();
        return result;
    }

    private async Task<IResult> FinishManualAsync(Fixture fx, IReadOnlyList<PeriodScore> periods)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var activeSanctions = new ActiveSanctions(context, new SanctionProgress(context), new SanctionFinePayments(context));

        var result = await RecordResult.HandleAsync(
            fx.MatchId, new RecordResult.Request(periods, Notes: null), context, new MatchRulesLookup(context),
            Policy, OutcomeRules, activeSanctions, OrganizationFor(fx.OrgId, fx.UserId), Clock, CancellationToken.None);

        await transaction.CommitAsync();
        return result;
    }

    private async Task<Match> ReadMatchAsync(Guid orgId, Guid matchId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);
        var match = await context.Matches.AsNoTracking().SingleAsync(candidate => candidate.Id == matchId);
        await transaction.CommitAsync();
        return match;
    }

    /// <summary>Plays out a clean 3-0 (25-0 three times, home winning every set) by events alone.</summary>
    private async Task PlayThreeCleanSetsAsync(Fixture fx)
    {
        for (short set = 1; set <= 3; set++)
        {
            for (var i = 0; i < 25; i++)
            {
                await RecordPointAsync(fx, fx.HomeEntryId, set);
            }
        }
    }

    // ---- 3a: a manual result and its own events must agree ---------------

    [Fact]
    public async Task HandleAsync_ManualResultMatchingEvents_IsAccepted()
    {
        var fx = await SeedAsync();
        await PlayThreeCleanSetsAsync(fx);

        var periods = new[]
        {
            new PeriodScore { Period = 1, Home = 25, Away = 0 },
            new PeriodScore { Period = 2, Home = 25, Away = 0 },
            new PeriodScore { Period = 3, Home = 25, Away = 0 },
        };

        Assert.IsType<Ok<RecordResult.Response>>(await FinishManualAsync(fx, periods));
    }

    [Fact]
    public async Task HandleAsync_ManualResultContradictingEvents_IsRefused()
    {
        var fx = await SeedAsync();
        await PlayThreeCleanSetsAsync(fx);

        // Events say home took every one of the first three sets 25-0.
        // This still reaches a valid 3-1 on its own (home takes 1, 3 and 4,
        // away takes 2) -- structurally complete, so this is purely about
        // the one set it contradicts, not about an incomplete match.
        var periods = new[]
        {
            new PeriodScore { Period = 1, Home = 25, Away = 0 },
            new PeriodScore { Period = 2, Home = 20, Away = 25 },
            new PeriodScore { Period = 3, Home = 25, Away = 0 },
            new PeriodScore { Period = 4, Home = 25, Away = 0 },
        };

        var result = await FinishManualAsync(fx, periods);

        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode.Should().Be(StatusCodes.Status409Conflict);
        (await ReadMatchAsync(fx.OrgId, fx.MatchId)).Status.Should().Be(MatchState.InProgress);
    }

    [Fact]
    public async Task HandleAsync_NoEventsRecordedYet_ManualResultIsAcceptedUnchecked()
    {
        // Nobody logged a single point live for this match — the recorder
        // chose to type the final score in by hand instead, exactly the
        // escape valve RecordResult.HandleFromEventsAsync's own remarks
        // describe. Nothing here to contradict.
        var fx = await SeedAsync();

        var periods = new[]
        {
            new PeriodScore { Period = 1, Home = 25, Away = 20 },
            new PeriodScore { Period = 2, Home = 25, Away = 18 },
            new PeriodScore { Period = 3, Home = 25, Away = 22 },
        };

        Assert.IsType<Ok<RecordResult.Response>>(await FinishManualAsync(fx, periods));
    }

    /// <summary>
    /// Pins the exact regression this phase was at risk of introducing: a
    /// cumulative sport's zero events still produce a non-empty, all-zero
    /// derived score (LiveScore.ComputePeriods' own documented behaviour),
    /// which must never be read as "events say 0-0" and refuse an
    /// unconnected manual score.
    /// </summary>
    [Fact]
    public async Task HandleAsync_CumulativeSport_NoEventsRecorded_NonZeroManualResultIsAccepted()
    {
        var fx = await SeedCumulativeAsync(pricesDraw: true, knockout: false);

        var result = await FinishManualAsync(
            fx, [new PeriodScore { Period = 1, Home = 1, Away = 0 }, new PeriodScore { Period = 2, Home = 2, Away = 1 }]);

        Assert.IsType<Ok<RecordResult.Response>>(result);
    }

    /// <summary>
    /// A minimal football fixture, with no roster or events — only what
    /// <see cref="FinishManualAsync"/> needs. <paramref name="pricesDraw"/>
    /// and <paramref name="knockout"/> exist for
    /// <see cref="RecordResult.RefuseUnpriceableTie"/>'s two conditions,
    /// which a volleyball fixture can never reach on its own
    /// (<see cref="SetsResultShape"/> never lets a period tie in the first
    /// place).
    /// </summary>
    private async Task<Fixture> SeedCumulativeAsync(bool pricesDraw, bool knockout)
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var homeClubId = Guid.NewGuid();
        var awayClubId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User { Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Planillero" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = homeClubId, OrgId = orgId, Name = "Club Local" });
        setup.Clubs.Add(new Club { Id = awayClubId, OrgId = orgId, Name = "Club Visitante" });
        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId, OrgId = orgId, SportCode = Football, Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = pricesDraw
                    ? new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 }
                    : new Dictionary<string, int> { ["win"] = 3, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });
        setup.Competitions.Add(new Competition
        {
            Id = competitionId, OrgId = orgId, SportCode = Football, RulesetId = rulesetId, Name = "Competencia",
            Slug = $"comp-{competitionId:N}", Season = "2026", Format = knockout ? "knockout" : "league",
            CaptureLevel = CaptureLevel.Detailed, Settings = new CompetitionSettings(),
        });
        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Categoria" });
        setup.Teams.Add(new Team { Id = homeTeamId, OrgId = orgId, ClubId = homeClubId, CategoryId = categoryId, Name = "Equipo Local" });
        setup.Teams.Add(new Team { Id = awayTeamId, OrgId = orgId, ClubId = awayClubId, CategoryId = categoryId, Name = "Equipo Visitante" });
        setup.Matches.Add(new Match
        {
            Id = matchId, OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = homeTeamId, AwayTeamId = awayTeamId, Status = MatchState.InProgress,
            Phase = knockout ? "Semifinal" : null,
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, userId, matchId, homeTeamId, awayTeamId, Guid.Empty, Guid.Empty, Guid.Empty, categoryId, competitionId);
    }

    /// <summary>
    /// A competition that only records the score (CaptureLevel.Basic) never
    /// has an event to read in the first place — EventPolicy.RefuseMatch
    /// already blocks writing one. "Finalizar con eventos" has to refuse
    /// for the same reason, not silently derive an all-zero cumulative
    /// score from a table that was never allowed to have rows.
    /// </summary>
    [Fact]
    public async Task HandleFromEventsAsync_BasicCaptureCompetition_IsRefused()
    {
        var fx = await SeedAsync(CaptureLevel.Basic);

        var result = await FinishFromEventsAsync(fx);

        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode.Should().Be(StatusCodes.Status409Conflict);
        (await ReadMatchAsync(fx.OrgId, fx.MatchId)).Status.Should().Be(MatchState.InProgress);
    }

    // ---- 3b: editing an event after the match is finished resyncs --------

    [Fact]
    public async Task ResyncAsync_CorrectingAnEventAfterFinish_RecalculatesTheScore()
    {
        var fx = await SeedAsync();
        await PlayThreeCleanSetsAsync(fx);
        var extraEventId = await RecordPointAsync(fx, fx.AwayEntryId, period: 1);

        Assert.IsType<Ok<RecordResult.Response>>(await FinishFromEventsAsync(fx));

        var finished = await ReadMatchAsync(fx.OrgId, fx.MatchId);
        finished.Status.Should().Be(MatchState.Finished);
        finished.PeriodScores!.Single(period => period.Period == 1).Away.Should().Be(1);

        // Somebody notices the away point in the first set was actually
        // theirs in the third — correcting it, long after the match was
        // finished, must move the score with it.
        await CorrectEventPeriodAsync(fx, extraEventId, period: 3);

        var resynced = await ReadMatchAsync(fx.OrgId, fx.MatchId);
        resynced.PeriodScores!.Single(period => period.Period == 1).Away.Should().Be(0);
        resynced.PeriodScores!.Single(period => period.Period == 3).Away.Should().Be(1);
    }

    [Fact]
    public async Task ResyncAsync_DeletingAnEventAfterFinish_RecalculatesTheScore()
    {
        var fx = await SeedAsync();
        await PlayThreeCleanSetsAsync(fx);
        var extraEventId = await RecordPointAsync(fx, fx.AwayEntryId, period: 1);

        await FinishFromEventsAsync(fx);
        (await ReadMatchAsync(fx.OrgId, fx.MatchId)).PeriodScores!.Single(period => period.Period == 1).Away.Should().Be(1);

        await DeleteEventAsync(fx, extraEventId);

        var resynced = await ReadMatchAsync(fx.OrgId, fx.MatchId);
        resynced.PeriodScores!.Single(period => period.Period == 1).Away.Should().Be(0);
        resynced.HomeTotal.Should().Be(3);
        resynced.AwayTotal.Should().Be(0);
    }

    [Fact]
    public async Task ResyncAsync_DeletingEveryEventOfADecidingSet_LeavesTheScoreUnchanged()
    {
        var fx = await SeedAsync();
        await PlayThreeCleanSetsAsync(fx);
        await FinishFromEventsAsync(fx);

        var beforeTampering = await ReadMatchAsync(fx.OrgId, fx.MatchId);
        beforeTampering.Status.Should().Be(MatchState.Finished);
        beforeTampering.HomeTotal.Should().Be(3);

        // Deleting every point of the third set removes it from
        // ComputePlayedPeriods entirely (no event left to place it by), so
        // the events now say only two sets were played — short of the three
        // ResultPolicy.Inspect requires, exactly what it would refuse if
        // this were typed in by hand. The event deletes themselves must
        // still succeed; only the resync they would otherwise trigger is
        // the part that is skipped, each time, until the last one leaves
        // the score exactly as this match was last finished with.
        List<Guid> thirdSetEventIds;
        await using (var context = fixture.CreateAppContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);
            thirdSetEventIds = await context.PlayerEvents.AsNoTracking()
                .Where(recorded => recorded.MatchId == fx.MatchId && recorded.PeriodNumber == 3)
                .Select(recorded => recorded.Id)
                .ToListAsync();
            await transaction.CommitAsync();
        }

        foreach (var eventId in thirdSetEventIds)
        {
            await DeleteEventAsync(fx, eventId);
        }

        var afterTampering = await ReadMatchAsync(fx.OrgId, fx.MatchId);
        afterTampering.Status.Should().Be(MatchState.Finished);
        afterTampering.HomeTotal.Should().Be(beforeTampering.HomeTotal);
        afterTampering.AwayTotal.Should().Be(beforeTampering.AwayTotal);
        afterTampering.PeriodScores.Should().BeEquivalentTo(beforeTampering.PeriodScores);
    }

    // ---- 4a: a set has to actually reach its target, not just be won ----

    [Fact]
    public async Task HandleAsync_ManualSetBelowTarget_IsRefused()
    {
        var fx = await SeedAsync();

        // A believable scoreline that is nonetheless never a real set: home
        // takes it 20-18, nowhere near 25.
        var periods = new[]
        {
            new PeriodScore { Period = 1, Home = 20, Away = 18 },
            new PeriodScore { Period = 2, Home = 25, Away = 10 },
            new PeriodScore { Period = 3, Home = 25, Away = 10 },
        };

        var result = await FinishManualAsync(fx, periods);

        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_ManualSetAtTargetWithNoMargin_IsRefused()
    {
        var fx = await SeedAsync();

        // 25-24 reaches the target but settles for a one-point lead, which
        // a real set never does -- play continues past 25 until somebody is
        // two points up.
        var periods = new[]
        {
            new PeriodScore { Period = 1, Home = 25, Away = 24 },
            new PeriodScore { Period = 2, Home = 25, Away = 10 },
            new PeriodScore { Period = 3, Home = 25, Away = 10 },
        };

        var result = await FinishManualAsync(fx, periods);

        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_DecidingSetUsesItsOwnLowerTarget_IsAccepted()
    {
        var fx = await SeedAsync();

        // Best of five, decided in the fifth at 15 rather than 25 -- the
        // fourth set would be refused at this score, the fifth is not.
        var periods = new[]
        {
            new PeriodScore { Period = 1, Home = 25, Away = 20 },
            new PeriodScore { Period = 2, Home = 20, Away = 25 },
            new PeriodScore { Period = 3, Home = 25, Away = 22 },
            new PeriodScore { Period = 4, Home = 22, Away = 25 },
            new PeriodScore { Period = 5, Home = 15, Away = 10 },
        };

        var result = await FinishManualAsync(fx, periods);

        Assert.IsType<Ok<RecordResult.Response>>(result);
    }

    [Fact]
    public async Task ResyncAsync_EventsLeaveASetBelowTarget_NeverFinishesTheMatch()
    {
        var fx = await SeedAsync();

        // Two clean sets, then a third that stalls at 20-18 -- home never
        // actually reaches the target the real third set would need to
        // close out the match 3-0.
        for (short set = 1; set <= 2; set++)
        {
            for (var i = 0; i < 25; i++)
            {
                await RecordPointAsync(fx, fx.HomeEntryId, set);
            }
        }
        for (var i = 0; i < 20; i++)
        {
            await RecordPointAsync(fx, fx.HomeEntryId, period: 3);
        }
        for (var i = 0; i < 18; i++)
        {
            await RecordPointAsync(fx, fx.AwayEntryId, period: 3);
        }

        var result = await FinishFromEventsAsync(fx);

        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await ReadMatchAsync(fx.OrgId, fx.MatchId)).Status.Should().Be(MatchState.InProgress);
    }

    /// <summary>
    /// A sets-mode sport with no point target at all — kyorugi's own asalto
    /// runs to the end of its clock, not to a score — must keep accepting
    /// whatever low tally that clock actually produces.
    /// Sport.TargetPointsPerSet is null for every sport but volleyball, and
    /// this is the one real existing sport this phase could have broken by
    /// assuming every sets-mode result races to 25.
    /// </summary>
    [Fact]
    public async Task HandleAsync_KyorugiRealisticLowScore_IsUnaffectedByTheVolleyballOnlyTarget()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var opponentClubId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User { Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Planillero" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club Rojo" });
        setup.Clubs.Add(new Club { Id = opponentClubId, OrgId = orgId, Name = "Club Azul" });
        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId, OrgId = orgId, SportCode = Kyorugi, Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 3, Label = "asalto", Minutes = 2 },
                Points = new Dictionary<string, int>
                {
                    ["win_2_0"] = 3, ["loss_0_2"] = 0, ["win_2_1"] = 3, ["loss_1_2"] = 1,
                },
                Tiebreakers = [],
            },
        });
        setup.Competitions.Add(new Competition
        {
            Id = competitionId, OrgId = orgId, SportCode = Kyorugi, RulesetId = rulesetId, Name = "Competencia",
            Slug = $"comp-{competitionId:N}", Season = "2026", Format = "knockout",
            CaptureLevel = CaptureLevel.Detailed, Settings = new CompetitionSettings(),
        });
        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Categoria" });
        setup.Teams.Add(new Team { Id = homeTeamId, OrgId = orgId, ClubId = clubId, CategoryId = categoryId, Name = "Rojo" });
        setup.Teams.Add(new Team { Id = awayTeamId, OrgId = orgId, ClubId = opponentClubId, CategoryId = categoryId, Name = "Azul" });
        setup.Matches.Add(new Match
        {
            Id = matchId, OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = homeTeamId, AwayTeamId = awayTeamId, Status = MatchState.InProgress,
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        var fx = new Fixture(orgId, userId, matchId, homeTeamId, awayTeamId, Guid.Empty, Guid.Empty, Guid.Empty, categoryId, competitionId);

        // Real-shaped kyorugi asalto scores: single digits, decided by
        // whoever led when the clock ran out, nowhere near a set's 25.
        var result = await FinishManualAsync(
            fx,
            [
                new PeriodScore { Period = 1, Home = 7, Away = 3 },
                new PeriodScore { Period = 2, Home = 4, Away = 9 },
                new PeriodScore { Period = 3, Home = 6, Away = 2 },
            ]);

        Assert.IsType<Ok<RecordResult.Response>>(result);
    }

    // ---- 4b: a tie with nowhere to go gets refused, not silently zeroed --

    [Fact]
    public async Task HandleAsync_CumulativeTieNoDrawPricedLeagueMatch_IsRefused()
    {
        var fx = await SeedCumulativeAsync(pricesDraw: false, knockout: false);

        var result = await FinishManualAsync(
            fx, [new PeriodScore { Period = 1, Home = 1, Away = 0 }, new PeriodScore { Period = 2, Home = 0, Away = 1 }]);

        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode.Should().Be(StatusCodes.Status409Conflict);
        (await ReadMatchAsync(fx.OrgId, fx.MatchId)).Status.Should().Be(MatchState.InProgress);
    }

    [Fact]
    public async Task HandleAsync_CumulativeTieDrawPricedLeagueMatch_IsAccepted()
    {
        var fx = await SeedCumulativeAsync(pricesDraw: true, knockout: false);

        var result = await FinishManualAsync(
            fx, [new PeriodScore { Period = 1, Home = 1, Away = 0 }, new PeriodScore { Period = 2, Home = 0, Away = 1 }]);

        Assert.IsType<Ok<RecordResult.Response>>(result);
    }

    [Fact]
    public async Task HandleAsync_CumulativeTieNoDrawPricedKnockoutMatch_IsAccepted()
    {
        // No table to credit, but RecordPenalties is waiting right behind
        // this -- a knockout tie is never a dead end the way a league one
        // without draw pricing is.
        var fx = await SeedCumulativeAsync(pricesDraw: false, knockout: true);

        var result = await FinishManualAsync(
            fx, [new PeriodScore { Period = 1, Home = 1, Away = 0 }, new PeriodScore { Period = 2, Home = 0, Away = 1 }]);

        Assert.IsType<Ok<RecordResult.Response>>(result);
    }

    // ---- 3b: never silently reroutes an already-advanced bracket ---------

    [Fact]
    public async Task ResyncAsync_WinnerWouldFlipButNextMatchAlreadyStarted_LeavesTheNextMatchUntouched()
    {
        var fx = await SeedAsync();
        await PlayThreeCleanSetsAsync(fx); // home wins 3-0
        await FinishFromEventsAsync(fx);

        var nextMatchId = Guid.NewGuid();
        await using (var context = fixture.CreateAppContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

            context.Matches.Add(new Match
            {
                Id = nextMatchId, OrgId = fx.OrgId, CompetitionId = fx.CompetitionId, CategoryId = fx.CategoryId,
                HomeSourceMatchId = fx.MatchId, HomeTeamId = fx.HomeTeamId, AwayTeamId = fx.AwayTeamId,
                Phase = "Final", Status = MatchState.InProgress, // already started with the original winner
            });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // Flips the whole result: away now wins every set 25-0 instead of
        // home — exactly the kind of correction that would otherwise
        // rewrite who plays the final.
        await using (var context = fixture.CreateAppContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

            var homeEvents = await context.PlayerEvents
                .Where(recorded => recorded.MatchId == fx.MatchId && recorded.RosterEntryId == fx.HomeEntryId)
                .ToListAsync();
            foreach (var recorded in homeEvents)
            {
                recorded.RosterEntryId = fx.AwayEntryId;
            }

            await context.SaveChangesAsync();
            await ResyncFor(context, fx.OrgId, fx.UserId).ResyncAsync(fx.MatchId, context, CancellationToken.None);

            await transaction.CommitAsync();
        }

        var nextMatch = await ReadMatchAsync(fx.OrgId, nextMatchId);
        nextMatch.HomeTeamId.Should().Be(fx.HomeTeamId); // untouched -- the final had already started with them
        nextMatch.Status.Should().Be(MatchState.InProgress);
    }

    [Fact]
    public async Task ResyncAsync_WinnerFlipsAndNextMatchStillScheduled_PropagatesTheNewWinner()
    {
        var fx = await SeedAsync();
        await PlayThreeCleanSetsAsync(fx); // home wins 3-0
        await FinishFromEventsAsync(fx);

        // The final's other side -- the winner of the other semifinal,
        // nothing to do with this match's own two teams, the same shape a
        // real bracket's next round has.
        var thirdTeamId = Guid.NewGuid();
        var nextMatchId = Guid.NewGuid();
        await using (var context = fixture.CreateAppContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

            var thirdClubId = Guid.NewGuid();
            context.Clubs.Add(new Club { Id = thirdClubId, OrgId = fx.OrgId, Name = "Club Rival" });
            context.Teams.Add(new Team
            {
                Id = thirdTeamId, OrgId = fx.OrgId, ClubId = thirdClubId, CategoryId = fx.CategoryId,
                Name = "Equipo Rival",
            });
            context.Matches.Add(new Match
            {
                Id = nextMatchId, OrgId = fx.OrgId, CompetitionId = fx.CompetitionId, CategoryId = fx.CategoryId,
                HomeSourceMatchId = fx.MatchId, AwayTeamId = thirdTeamId,
                Phase = "Final", Status = MatchState.Scheduled, // never started
            });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // Same flip as the test above, but with nothing downstream to
        // protect: propagation should run as normal.
        await using (var context = fixture.CreateAppContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

            var homeEvents = await context.PlayerEvents
                .Where(recorded => recorded.MatchId == fx.MatchId && recorded.RosterEntryId == fx.HomeEntryId)
                .ToListAsync();
            foreach (var recorded in homeEvents)
            {
                recorded.RosterEntryId = fx.AwayEntryId;
            }

            await context.SaveChangesAsync();
            await ResyncFor(context, fx.OrgId, fx.UserId).ResyncAsync(fx.MatchId, context, CancellationToken.None);

            await transaction.CommitAsync();
        }

        var decided = await ReadMatchAsync(fx.OrgId, fx.MatchId);
        decided.AwayTotal.Should().Be(3); // the resync did flip the winner here, with nothing to protect

        var nextMatch = await ReadMatchAsync(fx.OrgId, nextMatchId);
        nextMatch.HomeTeamId.Should().Be(fx.AwayTeamId); // propagated, since the final had not started yet
    }
}

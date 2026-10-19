using AwesomeAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Discipline;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Discipline;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Matches;

/// <summary>
/// What finishing a match surfaces about discipline — never acts on, only
/// names: a red card nobody has sanctioned yet for this match, and anyone
/// on either roster who already carries a sanction that blocks them today.
/// Both are read-only suggestions; neither changes whether the result
/// itself is accepted or how it is scored, which is why
/// <see cref="ResultPolicyTests"/> and this file test two different things
/// about the same endpoint without overlapping.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class RecordResultSanctionsTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // The real registries, built from the real per-mode implementations —
    // the same wiring ResultPolicyTests exercises without a container, used
    // here for the same reason: this is what Program.cs actually assembles.
    private static readonly IMatchOutcomeRulesRegistry OutcomeRules =
        new MatchOutcomeRulesRegistry(
            [new CumulativeMatchOutcomeRules(), new SetsMatchOutcomeRules(), new JudgedMatchOutcomeRules()]);

    private static readonly ResultPolicy Policy = new(
        new ResultShapeRulesRegistry(
            [new CumulativeResultShape(), new SetsResultShape(OutcomeRules), new JudgedResultShape()]));

    private sealed record Fixture(
        Guid OrgId, Guid UserId, Guid CompetitionId, Guid MatchId, Guid HomeTeamId, Guid AwayTeamId,
        Guid SentOffAthleteId, Guid RedCardMetricId);

    private static OrganizationContext OrganizationFor(Guid orgId, Guid userId)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Recorder, userId, ipAddress: null);
        return organization;
    }

    /// <summary>
    /// One organization, one football competition, two teams each with one
    /// registered player, and a scheduled match between them — enough to
    /// finish a result for real through <see cref="RecordResult.HandleAsync"/>,
    /// the same path Program.cs maps.
    /// </summary>
    private async Task<Fixture> SeedAsync()
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
        var sentOffAthleteId = Guid.NewGuid();
        var sanctionedAthleteId = Guid.NewGuid();
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
            SportCode = Football,
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
            SportCode = Football,
            RulesetId = rulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Categoria" });
        setup.Teams.Add(new Team { Id = homeTeamId, OrgId = orgId, ClubId = homeClubId, CategoryId = categoryId, Name = "Equipo Local" });
        setup.Teams.Add(new Team { Id = awayTeamId, OrgId = orgId, ClubId = awayClubId, CategoryId = categoryId, Name = "Equipo Visitante" });

        setup.Athletes.Add(new Athlete
        {
            Id = sentOffAthleteId, OrgId = orgId, FirstName = "Expulsado", LastName = "DePrueba",
            DocumentId = $"doc-{sentOffAthleteId:N}", BirthDate = new DateOnly(2000, 1, 1),
        });
        setup.Athletes.Add(new Athlete
        {
            Id = sanctionedAthleteId, OrgId = orgId, FirstName = "Sancionado", LastName = "DePrueba",
            DocumentId = $"doc-{sanctionedAthleteId:N}", BirthDate = new DateOnly(2000, 1, 1),
        });

        var sentOffEntryId = Guid.NewGuid();
        setup.RosterEntries.Add(new RosterEntry
        {
            Id = sentOffEntryId, OrgId = orgId, TeamId = homeTeamId, AthleteId = sentOffAthleteId,
        });
        setup.RosterEntries.Add(new RosterEntry
        {
            Id = Guid.NewGuid(), OrgId = orgId, TeamId = awayTeamId, AthleteId = sanctionedAthleteId,
        });

        setup.Matches.Add(new Match
        {
            Id = matchId,
            OrgId = orgId,
            CompetitionId = competitionId,
            CategoryId = categoryId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Status = MatchState.Scheduled,
        });

        // An unresolved sanction already on the books before this match —
        // nothing about finishing this one creates or changes it; the
        // warning only names that it exists.
        setup.Sanctions.Add(new Sanction
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            CompetitionId = competitionId,
            AthleteId = sanctionedAthleteId,
            TeamId = awayTeamId,
            Reason = "Agresión en la fecha anterior",
            CompletionRule = SanctionCompletionRule.Matches,
            MatchesRequired = 3,
            ImposedBy = userId,
            ImposedAt = Clock.GetUtcNow().AddDays(-7),
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        var redCardMetricId = await setup.SportMetrics
            .AsNoTracking()
            .Where(metric => metric.SportCode == Football && metric.Code == "red_card")
            .Select(metric => metric.Id)
            .SingleAsync();

        return new Fixture(
            orgId, userId, competitionId, matchId, homeTeamId, awayTeamId, sentOffAthleteId, redCardMetricId);
    }

    private async Task RecordRedCardAsync(Fixture fx, Guid rosterEntryId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.PlayerEvents.Add(new PlayerEvent
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            MatchId = fx.MatchId,
            RosterEntryId = rosterEntryId,
            MetricId = fx.RedCardMetricId,
            Quantity = 1,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<RecordResult.Response> FinishAsync(Fixture fx)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var activeSanctions = new ActiveSanctions(
            context, new SanctionProgress(context), new SanctionFinePayments(context));

        var request = new RecordResult.Request(
            [new PeriodScore { Period = 1, Home = 1, Away = 0 }, new PeriodScore { Period = 2, Home = 2, Away = 1 }],
            Notes: null);

        var result = await RecordResult.HandleAsync(
            fx.MatchId, request, context, new MatchRulesLookup(context), Policy, OutcomeRules, activeSanctions,
            OrganizationFor(fx.OrgId, fx.UserId), Clock, CancellationToken.None);
        await transaction.CommitAsync();

        return Assert.IsType<Ok<RecordResult.Response>>(result).Value!;
    }

    [Fact]
    public async Task Finish_WithARedCardAndAnUnrelatedActiveSanction_SurfacesBoth()
    {
        var fx = await SeedAsync();
        var sentOffEntryId = await RosterEntryIdAsync(fx.OrgId, fx.HomeTeamId, fx.SentOffAthleteId);
        await RecordRedCardAsync(fx, sentOffEntryId);

        var response = await FinishAsync(fx);

        var suggestion = response.SuggestedSanctions.Should().ContainSingle().Subject;
        suggestion.AthleteFirstName.Should().Be("Expulsado");
        suggestion.TeamId.Should().Be(fx.HomeTeamId);
        suggestion.MatchId.Should().Be(fx.MatchId);

        var warning = response.SuspendedPlayerWarnings.Should().ContainSingle().Subject;
        warning.AthleteFirstName.Should().Be("Sancionado");
        warning.TeamId.Should().Be(fx.AwayTeamId);
        warning.SanctionReason.Should().Be("Agresión en la fecha anterior");
    }

    [Fact]
    public async Task Finish_WithNoCardsAndNoActiveSanctions_SurfacesNeither()
    {
        var fx = await SeedUnsanctionedAsync();

        var response = await FinishAsync(fx);

        response.SuggestedSanctions.Should().BeEmpty();
        response.SuspendedPlayerWarnings.Should().BeEmpty();
    }

    /// <summary>
    /// Correcting an already-finished match's score must not keep suggesting
    /// a sanction for a card somebody already acted on — see
    /// <see cref="RecordResult.SuggestedSanction"/>'s own remarks on why this
    /// is matched by <see cref="Sanction.MatchId"/>.
    /// </summary>
    [Fact]
    public async Task Finish_ARedCardAlreadySanctionedForThisMatch_IsNotSuggestedAgain()
    {
        var fx = await SeedAsync();
        var sentOffEntryId = await RosterEntryIdAsync(fx.OrgId, fx.HomeTeamId, fx.SentOffAthleteId);
        await RecordRedCardAsync(fx, sentOffEntryId);
        await SeedExistingSanctionForMatchAsync(fx);

        var response = await FinishAsync(fx);

        response.SuggestedSanctions.Should().BeEmpty();
    }

    [Fact]
    public async Task Finish_AYellowCard_IsNeverSuggested()
    {
        var fx = await SeedAsync();
        var sentOffEntryId = await RosterEntryIdAsync(fx.OrgId, fx.HomeTeamId, fx.SentOffAthleteId);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);
        var yellowCardId = await context.SportMetrics.AsNoTracking()
            .Where(metric => metric.SportCode == Football && metric.Code == "yellow_card")
            .Select(metric => metric.Id)
            .SingleAsync();
        context.PlayerEvents.Add(new PlayerEvent
        {
            Id = Guid.NewGuid(), OrgId = fx.OrgId, MatchId = fx.MatchId, RosterEntryId = sentOffEntryId,
            MetricId = yellowCardId, Quantity = 1,
        });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        var response = await FinishAsync(fx);

        response.SuggestedSanctions.Should().BeEmpty();
    }

    private async Task<Guid> RosterEntryIdAsync(Guid orgId, Guid teamId, Guid athleteId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        return await context.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.TeamId == teamId && entry.AthleteId == athleteId)
            .Select(entry => entry.Id)
            .SingleAsync();
    }

    private async Task SeedExistingSanctionForMatchAsync(Fixture fx)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.Sanctions.Add(new Sanction
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            AthleteId = fx.SentOffAthleteId,
            TeamId = fx.HomeTeamId,
            MatchId = fx.MatchId,
            Reason = "Expulsión ya sancionada",
            CompletionRule = SanctionCompletionRule.Matches,
            MatchesRequired = 2,
            ImposedBy = fx.UserId,
            ImposedAt = Clock.GetUtcNow(),
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>A second fixture with no sanction seeded at all, for the "surfaces neither" case.</summary>
    private async Task<Fixture> SeedUnsanctionedAsync()
    {
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        // SeedAsync always plants one unrelated active sanction to exercise
        // the warning; this test needs none, so it is removed rather than
        // adding a parameter nobody else would use.
        await context.Sanctions.Where(sanction => sanction.CompetitionId == fx.CompetitionId)
            .ExecuteDeleteAsync();
        await transaction.CommitAsync();

        return fx;
    }
}

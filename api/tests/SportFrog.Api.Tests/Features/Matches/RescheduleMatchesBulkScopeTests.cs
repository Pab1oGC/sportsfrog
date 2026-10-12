using AwesomeAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Matches;

/// <summary>
/// The scope check <see cref="RescheduleMatchesBulk"/> runs over its whole
/// body before anything else: the targets come from the request, not a
/// route value <c>RequireScope</c> could gate, so the handler itself has to
/// refuse the batch — as a whole, before any space or conflict check — the
/// moment one move names a competition the caller's membership does not
/// reach.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class RescheduleMatchesBulkScopeTests(SportFrogDatabaseFixture fixture)
{
    /// <summary>
    /// Records what it is asked to enqueue rather than actually scheduling
    /// anything — there is no real Hangfire storage in this test, and
    /// nothing here needs one: <c>HandleAsync</c> only ever queues through
    /// <see cref="HttpResponse.OnCompleted"/>, which a bare
    /// <see cref="DefaultHttpContext"/> never fires outside a real response
    /// pipeline, so this is exercised only as a safety net.
    /// </summary>
    private sealed class RecordingBackgroundJobClient : IBackgroundJobClient
    {
        public List<Job> Enqueued { get; } = [];

        public string Create(Job job, IState state)
        {
            Enqueued.Add(job);
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    [Fact]
    public async Task A_scoped_membership_is_refused_when_one_move_names_a_competition_outside_its_scope()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId, competitionIds: [fx.CompetitionAId]);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await RescheduleMatchesBulk.HandleAsync(
            new RescheduleMatchesBulk.Request([
                new RescheduleMatchesBulk.MatchMove(fx.MatchAId, null, null, null),
                new RescheduleMatchesBulk.MatchMove(fx.MatchBId, null, null, null),
            ]),
            new DefaultHttpContext(),
            context,
            organization,
            new RecordingBackgroundJobClient(),
            CancellationToken.None);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task A_scoped_membership_reaching_every_competition_in_the_batch_is_not_refused()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId, competitionIds: [fx.CompetitionAId]);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        // A no-op move: every field set back to exactly what the match
        // already has, so nothing collides and nothing is written -- this
        // isolates the scope check from the conflict and space validation
        // around it, neither of which this test is about.
        var result = await RescheduleMatchesBulk.HandleAsync(
            new RescheduleMatchesBulk.Request([new RescheduleMatchesBulk.MatchMove(fx.MatchAId, null, null, null)]),
            new DefaultHttpContext(),
            context,
            organization,
            new RecordingBackgroundJobClient(),
            CancellationToken.None);

        result.Should().BeOfType<Ok<RescheduleMatchesBulk.Response>>();
    }

    [Fact]
    public async Task An_organization_wide_membership_is_not_refused_regardless_of_which_competitions_are_touched()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId, competitionIds: []);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await RescheduleMatchesBulk.HandleAsync(
            new RescheduleMatchesBulk.Request([
                new RescheduleMatchesBulk.MatchMove(fx.MatchAId, null, null, null),
                new RescheduleMatchesBulk.MatchMove(fx.MatchBId, null, null, null),
            ]),
            new DefaultHttpContext(),
            context,
            organization,
            new RecordingBackgroundJobClient(),
            CancellationToken.None);

        result.Should().BeOfType<Ok<RescheduleMatchesBulk.Response>>();
    }

    private static OrganizationContext Established(Guid orgId, IEnumerable<Guid> competitionIds)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);
        organization.EstablishScopes(new MembershipScopeSet(competitionIds.ToHashSet(), new HashSet<Guid>()));
        return organization;
    }

    private sealed record Fixture(Guid OrgId, Guid CompetitionAId, Guid MatchAId, Guid MatchBId);

    /// <summary>Two competitions, each with one scheduled match.</summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubXId = Guid.NewGuid();
        var clubYId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionAId = Guid.NewGuid();
        var competitionBId = Guid.NewGuid();
        var categoryAId = Guid.NewGuid();
        var categoryBId = Guid.NewGuid();
        var matchAId = Guid.NewGuid();
        var matchBId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubXId, OrgId = orgId, Name = "Club X" });
        setup.Clubs.Add(new Club { Id = clubYId, OrgId = orgId, Name = "Club Y" });

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

        foreach (var (competitionId, categoryId) in new[] { (competitionAId, categoryAId), (competitionBId, categoryBId) })
        {
            setup.Competitions.Add(new Competition
            {
                Id = competitionId,
                OrgId = orgId,
                SportCode = "football",
                RulesetId = rulesetId,
                Name = $"Competencia {competitionId:N}",
                Slug = $"comp-{competitionId:N}",
                Season = "2026",
                Format = "league",
                Settings = new CompetitionSettings(),
            });
            setup.Categories.Add(new Category
            {
                Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Categoria",
            });
        }

        var teamAHomeId = Guid.NewGuid();
        var teamAAwayId = Guid.NewGuid();
        var teamBHomeId = Guid.NewGuid();
        var teamBAwayId = Guid.NewGuid();

        setup.Teams.Add(new Team { Id = teamAHomeId, OrgId = orgId, ClubId = clubXId, CategoryId = categoryAId, Name = "A Local" });
        setup.Teams.Add(new Team { Id = teamAAwayId, OrgId = orgId, ClubId = clubYId, CategoryId = categoryAId, Name = "A Visita" });
        setup.Teams.Add(new Team { Id = teamBHomeId, OrgId = orgId, ClubId = clubXId, CategoryId = categoryBId, Name = "B Local" });
        setup.Teams.Add(new Team { Id = teamBAwayId, OrgId = orgId, ClubId = clubYId, CategoryId = categoryBId, Name = "B Visita" });

        setup.Matches.Add(new Match
        {
            Id = matchAId, OrgId = orgId, CompetitionId = competitionAId, CategoryId = categoryAId,
            HomeTeamId = teamAHomeId, AwayTeamId = teamAAwayId,
        });
        setup.Matches.Add(new Match
        {
            Id = matchBId, OrgId = orgId, CompetitionId = competitionBId, CategoryId = categoryBId,
            HomeTeamId = teamBHomeId, AwayTeamId = teamBAwayId,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, competitionAId, matchAId, matchBId);
    }
}

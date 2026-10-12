using AwesomeAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Performances;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Performances;

/// <summary>
/// The running-order sibling of
/// <see cref="Tests.Features.Matches.RescheduleMatchesBulkScopeTests"/>: the
/// scope check <see cref="RescheduleClassificationOrder"/> runs over its
/// whole body before anything else, for the same reason — these targets
/// come from the request, not a route value <c>RequireScope</c> could gate.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class RescheduleClassificationOrderScopeTests(SportFrogDatabaseFixture fixture)
{
    /// <summary>
    /// Records what it is asked to enqueue rather than actually scheduling
    /// anything — see <see cref="Tests.Features.Matches.RescheduleMatchesBulkScopeTests.RecordingBackgroundJobClient"/>'s
    /// own remarks on why a bare <see cref="DefaultHttpContext"/> never
    /// actually calls this.
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

        var result = await RescheduleClassificationOrder.HandleAsync(
            new RescheduleClassificationOrder.Request([
                new RescheduleClassificationOrder.PerformanceMove(fx.PerformanceAId, null, null, null),
                new RescheduleClassificationOrder.PerformanceMove(fx.PerformanceBId, null, null, null),
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

        // A no-op move, isolating the scope check from the conflict and
        // space validation around it -- neither is what this test is about.
        var result = await RescheduleClassificationOrder.HandleAsync(
            new RescheduleClassificationOrder.Request(
                [new RescheduleClassificationOrder.PerformanceMove(fx.PerformanceAId, null, null, null)]),
            new DefaultHttpContext(),
            context,
            organization,
            new RecordingBackgroundJobClient(),
            CancellationToken.None);

        result.Should().BeOfType<Ok<RescheduleClassificationOrder.Response>>();
    }

    [Fact]
    public async Task An_organization_wide_membership_is_not_refused_regardless_of_which_competitions_are_touched()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId, competitionIds: []);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await RescheduleClassificationOrder.HandleAsync(
            new RescheduleClassificationOrder.Request([
                new RescheduleClassificationOrder.PerformanceMove(fx.PerformanceAId, null, null, null),
                new RescheduleClassificationOrder.PerformanceMove(fx.PerformanceBId, null, null, null),
            ]),
            new DefaultHttpContext(),
            context,
            organization,
            new RecordingBackgroundJobClient(),
            CancellationToken.None);

        result.Should().BeOfType<Ok<RescheduleClassificationOrder.Response>>();
    }

    private static OrganizationContext Established(Guid orgId, IEnumerable<Guid> competitionIds)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);
        organization.EstablishScopes(new MembershipScopeSet(competitionIds.ToHashSet(), new HashSet<Guid>()));
        return organization;
    }

    private sealed record Fixture(Guid OrgId, Guid CompetitionAId, Guid PerformanceAId, Guid PerformanceBId);

    /// <summary>Two competitions, each with one scheduled performance.</summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionAId = Guid.NewGuid();
        var competitionBId = Guid.NewGuid();
        var categoryAId = Guid.NewGuid();
        var categoryBId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        var performanceAId = Guid.NewGuid();
        var performanceBId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
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

        foreach (var (competitionId, categoryId, teamId) in new[]
                 {
                     (competitionAId, categoryAId, teamAId),
                     (competitionBId, categoryBId, teamBId),
                 })
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
            setup.Teams.Add(new Team
            {
                Id = teamId, OrgId = orgId, ClubId = clubId, CategoryId = categoryId, Name = $"Equipo {teamId:N}",
            });
        }

        setup.Performances.Add(new Performance
        {
            Id = performanceAId, OrgId = orgId, CompetitionId = competitionAId, CategoryId = categoryAId, TeamId = teamAId,
        });
        setup.Performances.Add(new Performance
        {
            Id = performanceBId, OrgId = orgId, CompetitionId = competitionBId, CategoryId = categoryBId, TeamId = teamBId,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, competitionAId, performanceAId, performanceBId);
    }
}

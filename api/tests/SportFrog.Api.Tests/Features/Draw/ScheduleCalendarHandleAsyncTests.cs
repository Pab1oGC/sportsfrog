using AwesomeAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Draw;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Draw;

/// <summary>
/// The full <c>POST /competitions/{id}/schedule</c> handler: that it gates
/// and places from the jornada's own <see cref="CompetitionStage"/> now,
/// not from <c>Competition.Settings.Schedule</c>, which no longer exists.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ScheduleCalendarHandleAsyncTests(SportFrogDatabaseFixture fixture)
{
    private sealed class RecordingBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString();

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    private sealed record Seeded(Guid OrgId, Guid CompetitionId, Guid CategoryId);

    private async Task<Seeded> SeedAsync(Guid[]? spaceIds, short? bufferMinutes = null)
    {
        var orgId = Guid.NewGuid();
        var clubOneId = Guid.NewGuid();
        var clubTwoId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamOneId = Guid.NewGuid();
        var teamTwoId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubOneId, OrgId = orgId, Name = "Club Uno" });
        setup.Clubs.Add(new Club { Id = clubTwoId, OrgId = orgId, Name = "Club Dos" });

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
            Name = "Liga",
            Slug = $"liga-{competitionId:N}",
            Season = "2026",
            Format = CompetitionFormat.League,
            Settings = new CompetitionSettings(),
        });

        setup.CompetitionStages.Add(new CompetitionStage
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            CompetitionId = competitionId,
            Kind = StageKind.League,
            BufferMinutes = bufferMinutes,
            SpaceIds = spaceIds,
        });

        setup.Categories.Add(new Category
        {
            Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Primera",
        });

        setup.Teams.Add(new Team { Id = teamOneId, OrgId = orgId, ClubId = clubOneId, CategoryId = categoryId, Name = "Uno" });
        setup.Teams.Add(new Team { Id = teamTwoId, OrgId = orgId, ClubId = clubTwoId, CategoryId = categoryId, Name = "Dos" });

        // matches.venue_space_id lleva FK real a venue_spaces -- a diferencia
        // de competition_stages.space_ids (un uuid[] sin FK, igual que
        // ScheduleSettings.SpaceIds antes), un partido efectivamente
        // colocado necesita una cancha que exista de verdad.
        if (spaceIds is { Length: > 0 })
        {
            var venueId = Guid.NewGuid();
            setup.Venues.Add(new Venue { Id = venueId, OrgId = orgId, Name = "Sede" });

            foreach (var spaceId in spaceIds)
            {
                setup.VenueSpaces.Add(new VenueSpace { Id = spaceId, OrgId = orgId, VenueId = venueId, Name = $"Cancha {spaceId:N}" });
            }
        }

        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            CompetitionId = competitionId,
            CategoryId = categoryId,
            HomeTeamId = teamOneId,
            AwayTeamId = teamTwoId,
            RoundNumber = 1,
            Status = MatchState.Scheduled,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Seeded(orgId, competitionId, categoryId);
    }

    private static OrganizationContext ActingIn(Guid orgId)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);

        return organization;
    }

    private static Task<IResult> ScheduleCalendarHandle(
        Guid competitionId, SportFrog.Api.Infrastructure.Persistence.SportFrogDbContext context, Guid orgId) =>
        ScheduleCalendar.HandleAsync(
            competitionId,
            // Siempre en el futuro respecto del reloj real: JornadaWindow
            // refusa cualquier fecha ya pasada.
            new ScheduleCalendar.Request(
                From: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                StartTime: new TimeOnly(10, 0),
                UtcOffsetMinutes: 0),
            new DefaultHttpContext(),
            context,
            TimeProvider.System,
            ActingIn(orgId),
            new RecordingBackgroundJobClient(),
            CancellationToken.None);

    [Fact]
    public async Task HandleAsync_TheStageHasNoSpacesEnabled_IsAConflictNamingTheStage()
    {
        var seeded = await SeedAsync(spaceIds: null);

        await using var scope = fixture.CreateAppContext();
        await using var transaction = await scope.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(scope, seeded.OrgId);

        var result = await ScheduleCalendarHandle(seeded.CompetitionId, scope, seeded.OrgId);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Contains("fase", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task HandleAsync_TheStageHasASpaceEnabled_PlacesTheJornadaOnIt()
    {
        var spaceId = Guid.NewGuid();
        var seeded = await SeedAsync(spaceIds: [spaceId], bufferMinutes: 5);

        await using var scope = fixture.CreateAppContext();
        await using var transaction = await scope.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(scope, seeded.OrgId);

        var result = await ScheduleCalendarHandle(seeded.CompetitionId, scope, seeded.OrgId);

        var ok = Assert.IsType<Ok<ScheduleCalendar.Response>>(result);
        ok.Value!.Placed.Should().Be(1);
        ok.Value.Unplaced.Should().Be(0);

        var placed = await scope.Matches
            .AsNoTracking()
            .SingleAsync(match => match.CompetitionId == seeded.CompetitionId);
        placed.VenueSpaceId.Should().Be(spaceId);
        placed.ScheduledAt.Should().NotBeNull();
    }
}

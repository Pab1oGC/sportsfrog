using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Performances;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Performances;

/// <summary>
/// Whether a team's turn in a classification stage's running order can be
/// set: a mat that can hold it, a slot nothing else already claims. The
/// collision half is what a real request could not tell you before this
/// existed: a mat, day and turn already taken failed only once
/// SaveChangesAsync hit uq_performance_running_order, with no sentence
/// naming who already has it.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class PerformancePolicyTests(SportFrogDatabaseFixture fixture)
{
    private static readonly DateOnly Day1 = new(2026, 5, 1);

    private sealed record Fixture(
        Guid OrgId, Guid CompetitionId, Guid CategoryId, Guid VenueSpaceId, Guid TeamAId, Guid TeamBId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public PerformancePolicy Policy { get; } = new(context);

        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var venueSpaceId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubAId, OrgId = orgId, Name = "Club A" });
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });

        setup.Venues.Add(new Venue { Id = venueId, OrgId = orgId, Name = "Sede" });
        setup.VenueSpaces.Add(new VenueSpace { Id = venueSpaceId, OrgId = orgId, VenueId = venueId, Name = "Tapete 1" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = "taekwondo_poomsae",
            Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 1, Label = "ronda", Minutes = 1 },
                Points = new Dictionary<string, int>(),
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId,
            OrgId = orgId,
            SportCode = "taekwondo_poomsae",
            RulesetId = rulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Categoria" });

        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = categoryId, Name = "Equipo B" });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, competitionId, categoryId, venueSpaceId, teamAId, teamBId);
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    private static Performance Slotted(Fixture fx, Guid teamId, DateOnly day, short order) => new()
    {
        Id = Guid.NewGuid(),
        OrgId = fx.OrgId,
        CompetitionId = fx.CompetitionId,
        CategoryId = fx.CategoryId,
        TeamId = teamId,
        VenueSpaceId = fx.VenueSpaceId,
        ScheduledOn = day,
        OrderNumber = order,
    };

    [Fact]
    public async Task InspectAsync_SameMatSameDaySameTurn_AlreadyTaken_IsRejected()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        session.Context.Performances.Add(Slotted(fx, fx.TeamAId, Day1, 1));
        await session.Context.SaveChangesAsync();

        var violations = await session.Policy.InspectAsync(
            fx.VenueSpaceId, Day1, 1, excludingPerformanceId: Guid.NewGuid(), CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "OrderNumber");
    }

    [Fact]
    public async Task InspectAsync_SameMatSameDayDifferentTurn_IsAccepted()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        session.Context.Performances.Add(Slotted(fx, fx.TeamAId, Day1, 1));
        await session.Context.SaveChangesAsync();

        var violations = await session.Policy.InspectAsync(
            fx.VenueSpaceId, Day1, 2, excludingPerformanceId: Guid.NewGuid(), CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_EditingTheSamePerformanceUnchanged_DoesNotCollideWithItself()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var performance = Slotted(fx, fx.TeamAId, Day1, 1);
        session.Context.Performances.Add(performance);
        await session.Context.SaveChangesAsync();

        // SchedulePerformance siempre vuelve a inspeccionar la actuacion que
        // esta corrigiendo -- si no se excluyera a si misma, cualquier
        // correccion que no toque tapete, dia ni turno chocaria contra su
        // propia fila.
        var violations = await session.Policy.InspectAsync(
            fx.VenueSpaceId, Day1, 1, excludingPerformanceId: performance.Id, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_IncompleteSlot_SkipsCollisionCheckEntirely()
    {
        // Una actuacion sin tapete, dia o turno completos es ordinaria --
        // todavia no tiene con que chocar.
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        session.Context.Performances.Add(Slotted(fx, fx.TeamAId, Day1, 1));
        await session.Context.SaveChangesAsync();

        var violations = await session.Policy.InspectAsync(
            fx.VenueSpaceId, scheduledOn: null, orderNumber: null,
            excludingPerformanceId: Guid.NewGuid(), CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_UnknownVenueSpace_IsRejected()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var violations = await session.Policy.InspectAsync(
            Guid.NewGuid(), Day1, 1, excludingPerformanceId: Guid.NewGuid(), CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "VenueSpaceId");
    }
}

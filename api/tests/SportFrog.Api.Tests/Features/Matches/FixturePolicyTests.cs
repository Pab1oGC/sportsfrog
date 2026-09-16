using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Matches;

/// <summary>
/// Whether two teams can be put on a calendar together, at a place that can
/// hold them, at an instant nothing else already claims. The collision half
/// (<see cref="FixturePolicy.InspectAsync"/>'s scheduledAt/excludingMatchId
/// parameters) is what a real request could not tell you before this
/// existed: a ground and an instant already taken failed only once
/// SaveChangesAsync hit uq_space_schedule, and a team already committed at
/// that instant did not fail at all.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class FixturePolicyTests(SportFrogDatabaseFixture fixture)
{
    private static readonly DateTimeOffset At10 = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        Guid OrgId, Guid CompetitionId, Guid CategoryId, Guid VenueSpaceId,
        Guid TeamAId, Guid TeamBId, Guid TeamCId, Guid TeamDId);

    /// <summary>
    /// One RLS-scoped connection held open for the life of one test, same
    /// reason as <c>RosterPolicyTests.Session</c>: everything FixturePolicy
    /// reads has to share the transaction that carries app.current_org.
    /// </summary>
    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public FixturePolicy Policy { get; } = new(context);

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
        var clubCId = Guid.NewGuid();
        var clubDId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var venueSpaceId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        var teamCId = Guid.NewGuid();
        var teamDId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubAId, OrgId = orgId, Name = "Club A" });
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });
        setup.Clubs.Add(new Club { Id = clubCId, OrgId = orgId, Name = "Club C" });
        setup.Clubs.Add(new Club { Id = clubDId, OrgId = orgId, Name = "Club D" });

        setup.Venues.Add(new Venue { Id = venueId, OrgId = orgId, Name = "Sede" });
        setup.VenueSpaces.Add(new VenueSpace { Id = venueSpaceId, OrgId = orgId, VenueId = venueId, Name = "Cancha 1" });

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
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Categoria" });

        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = categoryId, Name = "Equipo B" });
        setup.Teams.Add(new Team { Id = teamCId, OrgId = orgId, ClubId = clubCId, CategoryId = categoryId, Name = "Equipo C" });
        setup.Teams.Add(new Team { Id = teamDId, OrgId = orgId, ClubId = clubDId, CategoryId = categoryId, Name = "Equipo D" });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, competitionId, categoryId, venueSpaceId, teamAId, teamBId, teamCId, teamDId);
    }

    /// <summary>
    /// A second competition, entirely unrelated to <see cref="Fixture"/>'s
    /// own — its own club, category and two teams — with one of its teams
    /// sharing a roster athlete with <see cref="Fixture.TeamAId"/>, already
    /// down for a match at <paramref name="at"/> on a ground of its own.
    /// What this sets up for: the same person, entered twice under two
    /// unrelated <c>Team</c> rows, cannot be scheduled onto two different
    /// grounds at the same instant — something comparing team ids alone
    /// would never see, because neither team id here is <see cref="Fixture.TeamAId"/>.
    /// </summary>
    private async Task SeedCrossCompetitionEntrantAsync(Fixture fx, DateTimeOffset at)
    {
        var clubEId = Guid.NewGuid();
        var clubFId = Guid.NewGuid();
        var competitionId2 = Guid.NewGuid();
        var rulesetId2 = Guid.NewGuid();
        var categoryId2 = Guid.NewGuid();
        var venueId2 = Guid.NewGuid();
        var venueSpaceId2 = Guid.NewGuid();
        var teamEId = Guid.NewGuid();
        var teamFId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, fx.OrgId);

        setup.Clubs.Add(new Club { Id = clubEId, OrgId = fx.OrgId, Name = "Club E" });
        setup.Clubs.Add(new Club { Id = clubFId, OrgId = fx.OrgId, Name = "Club F" });

        setup.Venues.Add(new Venue { Id = venueId2, OrgId = fx.OrgId, Name = "Otra sede" });
        setup.VenueSpaces.Add(new VenueSpace { Id = venueSpaceId2, OrgId = fx.OrgId, VenueId = venueId2, Name = "Cancha 2" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId2,
            OrgId = fx.OrgId,
            SportCode = "football",
            Name = "Reglamento 2",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId2,
            OrgId = fx.OrgId,
            SportCode = "football",
            RulesetId = rulesetId2,
            Name = "Otra competencia",
            Slug = $"comp-{competitionId2:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId2, OrgId = fx.OrgId, CompetitionId = competitionId2, Name = "Otra categoria" });

        setup.Teams.Add(new Team { Id = teamEId, OrgId = fx.OrgId, ClubId = clubEId, CategoryId = categoryId2, Name = "Equipo E" });
        setup.Teams.Add(new Team { Id = teamFId, OrgId = fx.OrgId, ClubId = clubFId, CategoryId = categoryId2, Name = "Equipo F" });

        setup.Athletes.Add(new Athlete
        {
            Id = athleteId,
            OrgId = fx.OrgId,
            FirstName = "Persona",
            LastName = "Compartida",
            DocumentId = $"doc-{athleteId:N}",
            BirthDate = new DateOnly(2005, 1, 1),
        });

        await setup.SaveChangesAsync();

        setup.RosterEntries.Add(new RosterEntry { Id = Guid.NewGuid(), OrgId = fx.OrgId, TeamId = fx.TeamAId, AthleteId = athleteId });
        setup.RosterEntries.Add(new RosterEntry { Id = Guid.NewGuid(), OrgId = fx.OrgId, TeamId = teamEId, AthleteId = athleteId });

        setup.Matches.Add(new Match
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            CompetitionId = competitionId2,
            CategoryId = categoryId2,
            HomeTeamId = teamEId,
            AwayTeamId = teamFId,
            VenueSpaceId = venueSpaceId2,
            ScheduledAt = at,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    private static Match Scheduled(Fixture fx, Guid homeTeamId, Guid awayTeamId, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        OrgId = fx.OrgId,
        CompetitionId = fx.CompetitionId,
        CategoryId = fx.CategoryId,
        HomeTeamId = homeTeamId,
        AwayTeamId = awayTeamId,
        VenueSpaceId = fx.VenueSpaceId,
        ScheduledAt = at,
    };

    [Fact]
    public async Task InspectAsync_SameGroundSameInstant_AlreadyTaken_IsRejected()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        session.Context.Matches.Add(Scheduled(fx, fx.TeamAId, fx.TeamBId, At10));
        await session.Context.SaveChangesAsync();

        var violations = await session.Policy.InspectAsync(
            fx.CategoryId, fx.TeamCId, fx.TeamDId, fx.VenueSpaceId, At10,
            excludingMatchId: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "VenueSpaceId");
    }

    [Fact]
    public async Task InspectAsync_SameGroundDifferentInstant_IsAccepted()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        session.Context.Matches.Add(Scheduled(fx, fx.TeamAId, fx.TeamBId, At10));
        await session.Context.SaveChangesAsync();

        var violations = await session.Policy.InspectAsync(
            fx.CategoryId, fx.TeamCId, fx.TeamDId, fx.VenueSpaceId, At10.AddHours(1),
            excludingMatchId: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_TeamAlreadyPlayingAtThatInstant_IsRejected()
    {
        // Nada en el esquema puede ver esto -- a diferencia de la cancha,
        // ningun equipo tiene una restriccion unica que lo respalde. Esta
        // es la unica red de seguridad que existe para el equipo.
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        session.Context.Matches.Add(Scheduled(fx, fx.TeamAId, fx.TeamBId, At10));
        await session.Context.SaveChangesAsync();

        var violations = await session.Policy.InspectAsync(
            fx.CategoryId, fx.TeamAId, fx.TeamCId, venueSpaceId: null, At10,
            excludingMatchId: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "HomeTeamId");
    }

    [Fact]
    public async Task InspectAsync_TeamSharesAnAthleteAlreadyPlayingElsewhere_AcrossCompetitions_IsRejected()
    {
        // El mismo deportista, anotado bajo un equipo de otra competencia
        // entera, ya esta jugando a esa hora -- en una cancha distinta, sin
        // que los dos equipos compartan ningun id. Comparar solo TeamId
        // nunca veria esto.
        var fx = await SeedAsync();
        await SeedCrossCompetitionEntrantAsync(fx, At10);
        await using var session = await OpenAsync(fx);

        var violations = await session.Policy.InspectAsync(
            fx.CategoryId, fx.TeamAId, fx.TeamBId, fx.VenueSpaceId, At10,
            excludingMatchId: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "HomeTeamId");
    }

    [Fact]
    public async Task InspectAsync_TeamSharesAnAthlete_ButAtADifferentInstant_IsAccepted()
    {
        var fx = await SeedAsync();
        await SeedCrossCompetitionEntrantAsync(fx, At10);
        await using var session = await OpenAsync(fx);

        var violations = await session.Policy.InspectAsync(
            fx.CategoryId, fx.TeamAId, fx.TeamBId, fx.VenueSpaceId, At10.AddHours(2),
            excludingMatchId: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_EditingTheSameMatchUnchanged_DoesNotCollideWithItself()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var match = Scheduled(fx, fx.TeamAId, fx.TeamBId, At10);
        session.Context.Matches.Add(match);
        await session.Context.SaveChangesAsync();

        // RescheduleMatch siempre vuelve a inspeccionar el partido que esta
        // corrigiendo -- si no se excluyera a si mismo, cualquier correccion
        // que no toque ni la cancha ni la hora chocaria contra su propia fila.
        var violations = await session.Policy.InspectAsync(
            fx.CategoryId, fx.TeamAId, fx.TeamBId, fx.VenueSpaceId, At10,
            excludingMatchId: match.Id, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_CancelledMatchAtThatGroundAndInstant_DoesNotCollide()
    {
        // Un partido cancelado deja de reclamar su cancha -- mismo criterio
        // que ya usa uq_space_schedule (WHERE status NOT IN cancelled,
        // postponed), aplicado tambien de este lado.
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var cancelled = Scheduled(fx, fx.TeamAId, fx.TeamBId, At10);
        cancelled.Status = MatchState.Cancelled;
        session.Context.Matches.Add(cancelled);
        await session.Context.SaveChangesAsync();

        var violations = await session.Policy.InspectAsync(
            fx.CategoryId, fx.TeamCId, fx.TeamDId, fx.VenueSpaceId, At10,
            excludingMatchId: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_NoScheduledAtGiven_SkipsCollisionCheckEntirely()
    {
        // Un partido con fecha para decidir despues es ordinario -- no tiene
        // todavia con que chocar, y no deberia fallar por eso.
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        session.Context.Matches.Add(Scheduled(fx, fx.TeamAId, fx.TeamBId, At10));
        await session.Context.SaveChangesAsync();

        var violations = await session.Policy.InspectAsync(
            fx.CategoryId, fx.TeamCId, fx.TeamDId, fx.VenueSpaceId, scheduledAt: null,
            excludingMatchId: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }
}

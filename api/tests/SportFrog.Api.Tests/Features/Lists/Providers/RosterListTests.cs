using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>Whether <see cref="RosterList"/> actually reads a team's squad — a query that
/// compiles can still fail the moment EF Core tries to translate it, which only shows up
/// against a real database.</summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class RosterListTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(Guid OrgId, Guid TeamId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>One team with an active, numbered player and a withdrawn, unnumbered one —
    /// enough to pin both the "available first" ordering and the "-" fallback for a missing
    /// shirt or position.</summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var activeAthleteId = Guid.NewGuid();
        var withdrawnAthleteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Test User",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club A" });

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
            Name = "Copa de Prueba",
            Slug = $"copa-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Primera" });
        setup.Teams.Add(new Team { Id = teamId, OrgId = orgId, ClubId = clubId, CategoryId = categoryId, Name = "Equipo A" });

        setup.Athletes.Add(new Athlete
        {
            Id = activeAthleteId, OrgId = orgId, FirstName = "Juan", LastName = "Diaz",
            DocumentId = $"doc-{activeAthleteId:N}", BirthDate = new DateOnly(2000, 1, 1),
        });
        setup.Athletes.Add(new Athlete
        {
            Id = withdrawnAthleteId, OrgId = orgId, FirstName = "Ana", LastName = "Soto",
            DocumentId = $"doc-{withdrawnAthleteId:N}", BirthDate = new DateOnly(1999, 6, 1),
        });

        await setup.SaveChangesAsync();

        setup.RosterEntries.Add(new RosterEntry
        {
            Id = Guid.NewGuid(), OrgId = orgId, TeamId = teamId, AthleteId = activeAthleteId,
            JerseyNumber = 9, Position = "Delantero",
        });
        setup.RosterEntries.Add(new RosterEntry
        {
            Id = Guid.NewGuid(), OrgId = orgId, TeamId = teamId, AthleteId = withdrawnAthleteId,
            WithdrawnAt = DateTimeOffset.UtcNow,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, teamId);
    }

    /// <summary>
    /// One individual entrant of a judged sport (poomsae), entered the only
    /// way an individual sport's team ever is — through EnrollIndividual,
    /// which never sets a jersey number or an on-field position.
    /// </summary>
    private async Task<Fixture> SeedIndividualAsync()
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Test User",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club A" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId, OrgId = orgId, SportCode = "taekwondo_poomsae", Name = "Reglamento Poomsae",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 1, Label = "ronda", Minutes = 1 },
                Points = new Dictionary<string, int>(),
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId, OrgId = orgId, SportCode = "taekwondo_poomsae", RulesetId = rulesetId,
            Name = "Competencia de Prueba", Slug = $"comp-{competitionId:N}", Season = "2026",
            Format = CompetitionFormat.League, Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Sub-15" });
        setup.Teams.Add(new Team
        {
            Id = teamId, OrgId = orgId, ClubId = clubId, CategoryId = categoryId, Name = "Diaz, Juan",
            IsIndividual = true,
        });

        setup.Athletes.Add(new Athlete
        {
            Id = athleteId, OrgId = orgId, FirstName = "Juan", LastName = "Diaz",
            DocumentId = $"doc-{athleteId:N}", BirthDate = new DateOnly(2000, 1, 1),
        });

        await setup.SaveChangesAsync();

        setup.RosterEntries.Add(new RosterEntry
        {
            Id = Guid.NewGuid(), OrgId = orgId, TeamId = teamId, AthleteId = athleteId,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, teamId);
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    [Fact]
    public async Task LoadAsync_UnknownTeam_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new RosterList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["teamId"] = Guid.NewGuid().ToString() }),
            session.Context,
            CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_NoTeamIdInScope_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new RosterList().LoadAsync(ListScope.Empty, session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_KnownTeam_TitleNamesTheTeam()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new RosterList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["teamId"] = fx.TeamId.ToString() }),
            session.Context,
            CancellationToken.None);

        table!.Title.Should().Be("Plantel — Equipo A");
    }

    [Fact]
    public async Task LoadAsync_AvailablePlayersComeBeforeWithdrawnOnes()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new RosterList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["teamId"] = fx.TeamId.ToString() }),
            session.Context,
            CancellationToken.None);

        var rows = table!.Sections.Single().Rows;

        rows.Should().HaveCount(2);

        var available = rows[0];
        available[0].Should().Be("9");
        available[1].Should().Be("Diaz, Juan");
        available[4].Should().Be("Delantero");
        available[5].Should().Be(false);

        var withdrawn = rows[1];
        withdrawn[1].Should().Be("Soto, Ana");
        withdrawn[5].Should().Be(true);
    }

    [Fact]
    public async Task LoadAsync_NoJerseyOrPosition_ShowsADash()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new RosterList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["teamId"] = fx.TeamId.ToString() }),
            session.Context,
            CancellationToken.None);

        var withdrawn = table!.Sections.Single().Rows[1];

        withdrawn[0].Should().Be("-");
        withdrawn[4].Should().Be("-");
    }

    /// <summary>
    /// An individual sport's entrant never has a jersey number or an
    /// on-field position — "Dorsal" and "Posición" are the exact kind of
    /// football-shaped noise this export should not carry into a taekwondo
    /// one, so both the column and the cell drop out entirely rather than
    /// showing a "-" nobody asked for.
    /// </summary>
    [Fact]
    public async Task LoadAsync_IndividualEntrant_DropsTheJerseyAndPositionColumns()
    {
        var fx = await SeedIndividualAsync();
        await using var session = await OpenAsync(fx);

        var table = await new RosterList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["teamId"] = fx.TeamId.ToString() }),
            session.Context,
            CancellationToken.None);

        table!.Columns.Select(column => column.Header).Should().Equal(
            "Apellido y nombre", "Documento", "Nacimiento", "Retirado");

        var row = table.Sections.Single().Rows.Single();
        row.Should().HaveCount(4);
        row[0].Should().Be("Diaz, Juan");
        row[3].Should().Be(false);
    }
}

using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Rosters.Import;
using SportFrog.Api.Features.Staff;
using SportFrog.Api.Features.Staff.Import;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Staff.Import;

/// <summary>
/// Says what an uploaded staff sheet would do, without doing any of it.
/// Expected outcomes below are worked out from what the file itself says
/// and from the two rules <see cref="StaffPolicy"/> already declares for
/// itself — not from reading StaffImportReview's private methods and
/// mirroring what they currently return.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class StaffImportReviewTests(SportFrogDatabaseFixture fixture)
{
    private static readonly DateOnly AnAdult = new(1980, 5, 14);

    private sealed record Fixture(Guid OrgId, Guid TeamId);

    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club de prueba" });

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

        setup.Categories.Add(new Category
        {
            Id = categoryId,
            OrgId = orgId,
            CompetitionId = competitionId,
            Name = "Categoria",
        });

        setup.Teams.Add(new Team
        {
            Id = teamId, OrgId = orgId, ClubId = clubId, CategoryId = categoryId, Name = "Equipo",
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, teamId);
    }

    private async Task<Athlete> CreateAthleteAsync(Guid orgId, string firstName, string lastName, string document)
    {
        var athlete = new Athlete
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            FirstName = firstName,
            LastName = lastName,
            DocumentId = document,
            BirthDate = AnAdult,
        };

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        context.Athletes.Add(athlete);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return athlete;
    }

    private async Task RegisterStaffAsync(
        Guid orgId, Guid teamId, Guid athleteId, StaffRole role, DateTimeOffset? withdrawnAt = null)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        context.TeamStaff.Add(new TeamStaff
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            TeamId = teamId,
            AthleteId = athleteId,
            Role = role,
            RegisteredAt = DateTimeOffset.UtcNow,
            WithdrawnAt = withdrawnAt,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<Team> LoadTeamAsync(Guid orgId, Guid teamId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        return await context.Teams.SingleAsync(candidate => candidate.Id == teamId);
    }

    /// <summary>
    /// Runs the review inside one RLS-scoped transaction: both it and
    /// StaffPolicy read through this same context, which carries
    /// <c>app.current_org</c> only for the life of the transaction.
    /// </summary>
    private async Task<StaffReviewedSheet> ReviewAsync(Guid orgId, Team team, params StaffSheetRow[] rows)
    {
        var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        return await new StaffImportReview(context, new StaffPolicy(context))
            .ReviewAsync(team, rows, CancellationToken.None);
    }

    private static StaffSheetRow Row(
        int number, string document, string lastName, string firstName, string roleLabel,
        DateOnly? birthDate = null) =>
        new(number, document, lastName, firstName, birthDate ?? AnAdult, null, roleLabel, StaffSheet.RoleOf(roleLabel));

    // ---- The ordinary cases ----------------------------------------------

    [Fact]
    public async Task ReviewAsync_NewPerson_IsCreateAndRegister()
    {
        var fx = await SeedAsync();
        var team = await LoadTeamAsync(fx.OrgId, fx.TeamId);
        var rows = new[] { Row(2, "DOC-1", "Rojas", "Ana", "Director técnico") };

        var reviewed = await ReviewAsync(fx.OrgId, team, rows);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.CreateAndRegister);
    }

    [Fact]
    public async Task ReviewAsync_ExistingAthleteByDocument_NotYetOnThisTeam_IsRegister()
    {
        var fx = await SeedAsync();
        var team = await LoadTeamAsync(fx.OrgId, fx.TeamId);
        var existing = await CreateAthleteAsync(fx.OrgId, "Beto", "Gomez", "DOC-2");
        var rows = new[] { Row(2, existing.DocumentId, "Gomez", "Beto", "Médico") };

        var reviewed = await ReviewAsync(fx.OrgId, team, rows);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.Register);
    }

    // ---- Already on the bench ---------------------------------------------

    [Fact]
    public async Task ReviewAsync_AlreadyActiveOnThisTeam_IsAlreadyRegistered()
    {
        var fx = await SeedAsync();
        var team = await LoadTeamAsync(fx.OrgId, fx.TeamId);
        var athlete = await CreateAthleteAsync(fx.OrgId, "Carla", "Diaz", "DOC-3");
        await RegisterStaffAsync(fx.OrgId, fx.TeamId, athlete.Id, StaffRole.PhysicalTrainer);
        var rows = new[] { Row(2, athlete.DocumentId, "Diaz", "Carla", "Preparador físico") };

        var reviewed = await ReviewAsync(fx.OrgId, team, rows);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.AlreadyRegistered);
    }

    /// <summary>
    /// A withdrawal does not free the person for a silent reinstatement
    /// through a bulk file — see <c>StaffPolicy.InspectAsync</c>, whose
    /// message this surfaces unchanged.
    /// </summary>
    [Fact]
    public async Task ReviewAsync_WithdrawnFromThisTeam_IsRejectedRatherThanSilentlyReinstated()
    {
        var fx = await SeedAsync();
        var team = await LoadTeamAsync(fx.OrgId, fx.TeamId);
        var athlete = await CreateAthleteAsync(fx.OrgId, "Dario", "Luna", "DOC-4");
        await RegisterStaffAsync(
            fx.OrgId, fx.TeamId, athlete.Id, StaffRole.Delegate, withdrawnAt: DateTimeOffset.UtcNow);
        var rows = new[] { Row(2, athlete.DocumentId, "Luna", "Dario", "Delegado") };

        var reviewed = await ReviewAsync(fx.OrgId, team, rows);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.Rejected);
    }

    // ---- Row shape ----------------------------------------------------------

    [Fact]
    public async Task ReviewAsync_MissingRequiredField_IsRejected()
    {
        var fx = await SeedAsync();
        var team = await LoadTeamAsync(fx.OrgId, fx.TeamId);
        var rows = new[] { new StaffSheetRow(2, null, "Vera", "Saul", AnAdult, null, "Delegado", StaffRole.Delegate) };

        var reviewed = await ReviewAsync(fx.OrgId, team, rows);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.Rejected);
    }

    [Fact]
    public async Task ReviewAsync_UnrecognizedRole_IsRejected()
    {
        var fx = await SeedAsync();
        var team = await LoadTeamAsync(fx.OrgId, fx.TeamId);
        var rows = new[] { Row(2, "DOC-5", "Toro", "Quim", "Entrenador en jefe") };

        var reviewed = await ReviewAsync(fx.OrgId, team, rows);

        var row = reviewed.Rows.Single();
        row.Outcome.Should().Be(ImportOutcome.Rejected);
        row.Problems.Should().ContainSingle(p => p.Contains("cargo reconocido"));
    }

    [Fact]
    public async Task ReviewAsync_SameDocumentTwiceInFile_SecondRowIsRejected()
    {
        var fx = await SeedAsync();
        var team = await LoadTeamAsync(fx.OrgId, fx.TeamId);
        var rows = new[]
        {
            Row(2, "DOC-6", "Iban", "Nora", "Kinesiólogo"),
            Row(3, "DOC-6", "Iban", "Nora", "Kinesiólogo"),
        };

        var reviewed = await ReviewAsync(fx.OrgId, team, rows);

        reviewed.Rows[0].Outcome.Should().Be(ImportOutcome.CreateAndRegister);
        reviewed.Rows[1].Outcome.Should().Be(ImportOutcome.Rejected);
    }

    // ---- One head coach at a time -----------------------------------------

    [Fact]
    public async Task ReviewAsync_HeadCoachAlreadyRegistered_NewHeadCoachRowIsRejected()
    {
        var fx = await SeedAsync();
        var team = await LoadTeamAsync(fx.OrgId, fx.TeamId);
        var current = await CreateAthleteAsync(fx.OrgId, "Mario", "Vargas", "DOC-7");
        await RegisterStaffAsync(fx.OrgId, fx.TeamId, current.Id, StaffRole.HeadCoach);
        var rows = new[] { Row(2, "DOC-8", "Quispe", "Luis", "Director técnico") };

        var reviewed = await ReviewAsync(fx.OrgId, team, rows);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.Rejected);
    }

    /// <summary>
    /// Two rows naming a first head coach, neither yet in the database: the
    /// one fact an import knows that a single registration does not.
    /// </summary>
    [Fact]
    public async Task ReviewAsync_TwoHeadCoachRowsInTheSameFile_SecondIsRejected()
    {
        var fx = await SeedAsync();
        var team = await LoadTeamAsync(fx.OrgId, fx.TeamId);
        var rows = new[]
        {
            Row(2, "DOC-9", "Fierro", "Lucas", "Director técnico"),
            Row(3, "DOC-10", "Mora", "Pia", "Director técnico"),
        };

        var reviewed = await ReviewAsync(fx.OrgId, team, rows);

        reviewed.Rows[0].Outcome.Should().Be(ImportOutcome.CreateAndRegister);
        reviewed.Rows[1].Outcome.Should().Be(ImportOutcome.Rejected);
    }

    /// <summary>Only the head coach is capped; a bench has as many assistants as it likes.</summary>
    [Fact]
    public async Task ReviewAsync_TwoAssistantCoachRowsInTheSameFile_BothAccepted()
    {
        var fx = await SeedAsync();
        var team = await LoadTeamAsync(fx.OrgId, fx.TeamId);
        var rows = new[]
        {
            Row(2, "DOC-11", "Cano", "Mario", "Asistente técnico"),
            Row(3, "DOC-12", "Leon", "Omar", "Asistente técnico"),
        };

        var reviewed = await ReviewAsync(fx.OrgId, team, rows);

        reviewed.Rows.Should().OnlyContain(row => row.Outcome == ImportOutcome.CreateAndRegister);
    }
}

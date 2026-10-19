using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Features.Staff;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Staff;

/// <summary>
/// Whether a person may be on a team's staff, in the role they were given.
/// Expected results below come from the two rules the table declares for
/// itself — one staff row per person per team among the living, and one
/// director técnico at a time — not from reading InspectAsync and mirroring
/// what it currently returns.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class StaffPolicyTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(Guid OrgId, Guid TeamAId, Guid TeamBId);

    /// <summary>
    /// One organization, two clubs each entered as their own team of the same
    /// category — enough for both rules, none of it specific to any one test.
    /// </summary>
    /// <remarks>
    /// The whole chain above a team is seeded because team_staff is only
    /// visible while its team and the competition above it are: the query
    /// filter reads through the chain rather than copying anything down it.
    /// </remarks>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();

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

        setup.Clubs.Add(new Club { Id = clubAId, OrgId = orgId, Name = "Club A" });
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });

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

        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = categoryId, Name = "Equipo B" });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, teamAId, teamBId);
    }

    /// <summary>
    /// A person of the organization. Staff come from the same table athletes
    /// do, which is the point of it — see <see cref="TeamStaff"/>.
    /// </summary>
    private async Task<Athlete> CreatePersonAsync(Guid orgId, string firstName, string lastName)
    {
        var person = new Athlete
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            FirstName = firstName,
            LastName = lastName,
            DocumentId = $"DOC-{Guid.NewGuid():N}",
            BirthDate = new DateOnly(1980, 5, 14),
        };

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        context.Athletes.Add(person);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return person;
    }

    private async Task<TeamStaff> RegisterAsync(
        Guid orgId, Guid teamId, Guid athleteId, StaffRole role, DateTimeOffset? withdrawnAt = null)
    {
        var staff = new TeamStaff
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            TeamId = teamId,
            AthleteId = athleteId,
            Role = role,
            RegisteredAt = DateTimeOffset.UtcNow,
            WithdrawnAt = withdrawnAt,
        };

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        context.TeamStaff.Add(staff);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return staff;
    }

    /// <summary>
    /// Runs the policy inside one RLS-scoped transaction: every row it reads
    /// carries row-level security, so the question has to be asked inside the
    /// transaction that carries <c>app.current_org</c> — that setting does not
    /// survive past it.
    /// </summary>
    private async Task<IReadOnlyList<StaffViolation>> InspectAsync(
        Guid orgId, Guid teamId, Guid athleteId, StaffRole role, Guid? excluding = null)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        return await new StaffPolicy(context)
            .InspectAsync(teamId, athleteId, role, excluding, CancellationToken.None);
    }

    // ---- One staff row per person per team -------------------------------

    [Fact]
    public async Task InspectAsync_PersonNotOnTheStaff_IsAccepted()
    {
        var fx = await SeedAsync();
        var person = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");

        var violations = await InspectAsync(fx.OrgId, fx.TeamAId, person.Id, StaffRole.HeadCoach);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_PersonAlreadyOnTheStaff_IsRefused()
    {
        var fx = await SeedAsync();
        var person = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");
        await RegisterAsync(fx.OrgId, fx.TeamAId, person.Id, StaffRole.AssistantCoach);

        var violations = await InspectAsync(fx.OrgId, fx.TeamAId, person.Id, StaffRole.PhysicalTrainer);

        violations.Should().ContainSingle(violation => violation.Property == "AthleteId");
    }

    /// <summary>
    /// A withdrawal does not free the person: they genuinely worked here, and
    /// the way back is to reinstate the row rather than write a second one —
    /// which is exactly what uq_team_staff_team_athlete enforces by filtering
    /// on deleted_at alone.
    /// </summary>
    [Fact]
    public async Task InspectAsync_PersonWhoWithdrewFromTheStaff_IsStillRefused()
    {
        var fx = await SeedAsync();
        var person = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");
        await RegisterAsync(
            fx.OrgId, fx.TeamAId, person.Id, StaffRole.AssistantCoach, withdrawnAt: DateTimeOffset.UtcNow);

        var violations = await InspectAsync(fx.OrgId, fx.TeamAId, person.Id, StaffRole.AssistantCoach);

        violations.Should().ContainSingle(violation => violation.Property == "AthleteId");
    }

    [Fact]
    public async Task InspectAsync_SamePersonOnAnotherTeamsStaff_IsAccepted()
    {
        var fx = await SeedAsync();
        var person = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");
        await RegisterAsync(fx.OrgId, fx.TeamAId, person.Id, StaffRole.HeadCoach);

        var violations = await InspectAsync(fx.OrgId, fx.TeamBId, person.Id, StaffRole.HeadCoach);

        violations.Should().BeEmpty();
    }

    // ---- One head coach at a time ----------------------------------------

    [Fact]
    public async Task InspectAsync_SecondHeadCoachOfTheSameTeam_IsRefused()
    {
        var fx = await SeedAsync();
        var first = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");
        var second = await CreatePersonAsync(fx.OrgId, "Luis", "Quispe");
        await RegisterAsync(fx.OrgId, fx.TeamAId, first.Id, StaffRole.HeadCoach);

        var violations = await InspectAsync(fx.OrgId, fx.TeamAId, second.Id, StaffRole.HeadCoach);

        violations.Should().ContainSingle(violation => violation.Property == "Role");
    }

    /// <summary>
    /// The seat a coach vacates belongs to whoever takes it: a team changing
    /// coach mid-season withdraws the first and registers the second.
    /// </summary>
    [Fact]
    public async Task InspectAsync_NewHeadCoach_AfterTheFirstWithdrew_IsAccepted()
    {
        var fx = await SeedAsync();
        var first = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");
        var second = await CreatePersonAsync(fx.OrgId, "Luis", "Quispe");
        await RegisterAsync(
            fx.OrgId, fx.TeamAId, first.Id, StaffRole.HeadCoach, withdrawnAt: DateTimeOffset.UtcNow);

        var violations = await InspectAsync(fx.OrgId, fx.TeamAId, second.Id, StaffRole.HeadCoach);

        violations.Should().BeEmpty();
    }

    /// <summary>
    /// Only the head coach is capped. A bench has as many assistants,
    /// physios and delegates as the organizer decides.
    /// </summary>
    [Fact]
    public async Task InspectAsync_SecondAssistantCoachOfTheSameTeam_IsAccepted()
    {
        var fx = await SeedAsync();
        var first = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");
        var second = await CreatePersonAsync(fx.OrgId, "Luis", "Quispe");
        await RegisterAsync(fx.OrgId, fx.TeamAId, first.Id, StaffRole.AssistantCoach);

        var violations = await InspectAsync(fx.OrgId, fx.TeamAId, second.Id, StaffRole.AssistantCoach);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_HeadCoachOfAnotherTeam_DoesNotTakeThisTeamsSeat()
    {
        var fx = await SeedAsync();
        var elsewhere = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");
        var candidate = await CreatePersonAsync(fx.OrgId, "Luis", "Quispe");
        await RegisterAsync(fx.OrgId, fx.TeamBId, elsewhere.Id, StaffRole.HeadCoach);

        var violations = await InspectAsync(fx.OrgId, fx.TeamAId, candidate.Id, StaffRole.HeadCoach);

        violations.Should().BeEmpty();
    }

    /// <summary>
    /// Correcting a row's own role must not make it collide with itself —
    /// which is what <c>excluding</c> is for.
    /// </summary>
    [Fact]
    public async Task InspectAsync_PromotingTheOnlyAssistantToHeadCoach_IsAccepted()
    {
        var fx = await SeedAsync();
        var person = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");
        var staff = await RegisterAsync(fx.OrgId, fx.TeamAId, person.Id, StaffRole.AssistantCoach);

        var violations = await InspectAsync(
            fx.OrgId, fx.TeamAId, person.Id, StaffRole.HeadCoach, excluding: staff.Id);

        violations.Should().BeEmpty();
    }

    // ---- The database holds the same two rules ---------------------------

    /// <summary>
    /// The policy answers the ordinary case with a sentence somebody can act
    /// on; this is the guarantee underneath it, which is what actually holds
    /// two simultaneous requests to one answer.
    /// </summary>
    [Fact]
    public async Task TheDatabase_RefusesASecondHeadCoach_EvenWithoutThePolicy()
    {
        var fx = await SeedAsync();
        var first = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");
        var second = await CreatePersonAsync(fx.OrgId, "Luis", "Quispe");
        await RegisterAsync(fx.OrgId, fx.TeamAId, first.Id, StaffRole.HeadCoach);

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(
            () => RegisterAsync(fx.OrgId, fx.TeamAId, second.Id, StaffRole.HeadCoach));

        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("uq_team_staff_head_coach", postgres.ConstraintName);
    }

    [Fact]
    public async Task TheDatabase_RefusesTheSamePersonTwiceOnOneTeam()
    {
        var fx = await SeedAsync();
        var person = await CreatePersonAsync(fx.OrgId, "Mario", "Vargas");
        await RegisterAsync(fx.OrgId, fx.TeamAId, person.Id, StaffRole.AssistantCoach);

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(
            () => RegisterAsync(fx.OrgId, fx.TeamAId, person.Id, StaffRole.Physician));

        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("uq_team_staff_team_athlete", postgres.ConstraintName);
    }
}

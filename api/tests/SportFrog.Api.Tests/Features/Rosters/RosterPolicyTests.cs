using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Rosters;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Rosters;

/// <summary>
/// Whether a person may be registered for a team, and in the shirt they were
/// given. Expected results below come from the eligibility rules a category
/// declares for itself — sex, a window of birth dates, a roster cap, one
/// shirt per number — not from reading InspectAsync and mirroring what it
/// currently returns.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class RosterPolicyTests(SportFrogDatabaseFixture fixture)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Fixture(Guid OrgId, Guid CategoryId, Guid TeamAId, Guid TeamBId);

    /// <summary>
    /// One RLS-scoped connection, held open for the life of one test: every
    /// row RosterPolicy needs to see — the team, the category, whatever
    /// registrations the test set up — has to be read inside the same
    /// transaction that carries <c>app.current_org</c>, since that setting
    /// does not survive past it.
    /// </summary>
    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public RosterPolicy Policy { get; } = new(context);

        public Team Team { get; set; } = null!;

        public Category Category { get; set; } = null!;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>
    /// One organization, two clubs each entered as their own team of the
    /// same category — enough scaffolding for every eligibility rule
    /// RosterPolicy enforces, none of it specific to any one test.
    /// </summary>
    private async Task<Fixture> SeedAsync(Action<Category>? configureCategory = null)
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

        var category = new Category
        {
            Id = categoryId,
            OrgId = orgId,
            CompetitionId = competitionId,
            Name = "Categoria",
        };
        configureCategory?.Invoke(category);
        setup.Categories.Add(category);

        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = categoryId, Name = "Equipo B" });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, categoryId, teamAId, teamBId);
    }

    private async Task<Athlete> CreateAthleteAsync(
        Guid orgId, string firstName, string lastName, DateOnly birthDate, string? gender = null, bool isActive = true)
    {
        var athlete = new Athlete
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            FirstName = firstName,
            LastName = lastName,
            DocumentId = $"DOC-{Guid.NewGuid():N}",
            BirthDate = birthDate,
            Gender = gender,
            IsActive = isActive,
        };

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        context.Athletes.Add(athlete);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return athlete;
    }

    private async Task<RosterEntry> RegisterAsync(
        Guid orgId, Guid teamId, Guid athleteId, short? jerseyNumber = null, DateTimeOffset? withdrawnAt = null)
    {
        var entry = new RosterEntry
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            TeamId = teamId,
            AthleteId = athleteId,
            JerseyNumber = jerseyNumber,
            RegisteredAt = DateTimeOffset.UtcNow,
            WithdrawnAt = withdrawnAt,
        };

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        context.RosterEntries.Add(entry);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return entry;
    }

    /// <summary>
    /// Opens the one connection a test runs its policy checks through, with
    /// the organization's context set and held for as long as the session
    /// stays open.
    /// </summary>
    private async Task<Session> OpenAsync(Fixture fx, Guid teamId)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var session = new Session(context, transaction)
        {
            Team = await context.Teams.SingleAsync(t => t.Id == teamId),
            Category = await context.Categories.SingleAsync(c => c.Id == fx.CategoryId),
        };

        return session;
    }

    // ---- Athlete eligibility: sex ---------------------------------------

    [Fact]
    public async Task InspectAsync_CategoryAdmitsOnlyOneSex_AthleteOfThatSex_IsAccepted()
    {
        var fx = await SeedAsync(category => category.Gender = "F");
        var athlete = await CreateAthleteAsync(fx.OrgId, "Ana", "Rojas", Today.AddYears(-20), gender: "F");
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_CategoryAdmitsOnlyOneSex_AthleteOfTheOtherSex_IsRejected()
    {
        var fx = await SeedAsync(category => category.Gender = "F");
        var athlete = await CreateAthleteAsync(fx.OrgId, "Beto", "Gomez", Today.AddYears(-20), gender: "M");
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "AthleteId");
    }

    [Fact]
    public async Task InspectAsync_CategoryAdmitsOnlyOneSex_AthleteWithNoSexRecorded_IsRejected()
    {
        // Cannot be shown to fail the restriction, but cannot be shown to
        // pass it either -- a category drawn along that line was drawn for a
        // reason, so the missing fact is refused rather than waved through.
        var fx = await SeedAsync(category => category.Gender = "F");
        var athlete = await CreateAthleteAsync(fx.OrgId, "Cami", "Diaz", Today.AddYears(-20), gender: null);
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "AthleteId");
    }

    [Fact]
    public async Task InspectAsync_CategoryHasNoSexRestriction_AnySexIsAccepted()
    {
        var fx = await SeedAsync(); // Gender left null
        var athlete = await CreateAthleteAsync(fx.OrgId, "Dario", "Lima", Today.AddYears(-20), gender: null);
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    // ---- Athlete eligibility: birth date window ---------------------

    [Fact]
    public async Task InspectAsync_AthleteBornBeforeTheEarliestAdmitted_IsRejected()
    {
        var earliest = Today.AddYears(-18);
        var fx = await SeedAsync(category => category.BirthDateFrom = earliest);
        var athlete = await CreateAthleteAsync(fx.OrgId, "Elias", "Vega", earliest.AddDays(-1));
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "AthleteId");
    }

    [Fact]
    public async Task InspectAsync_AthleteBornExactlyOnTheEarliestAdmitted_IsAccepted()
    {
        // The boundary itself is admitted -- "earliest" means the oldest
        // player the category still takes, not the day before it.
        var earliest = Today.AddYears(-18);
        var fx = await SeedAsync(category => category.BirthDateFrom = earliest);
        var athlete = await CreateAthleteAsync(fx.OrgId, "Fer", "Ortiz", earliest);
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_AthleteBornAfterTheLatestAdmitted_IsRejected()
    {
        var latest = Today.AddYears(-14);
        var fx = await SeedAsync(category => category.BirthDateTo = latest);
        var athlete = await CreateAthleteAsync(fx.OrgId, "Gaby", "Nunez", latest.AddDays(1));
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "AthleteId");
    }

    [Fact]
    public async Task InspectAsync_AthleteBornExactlyOnTheLatestAdmitted_IsAccepted()
    {
        var latest = Today.AddYears(-14);
        var fx = await SeedAsync(category => category.BirthDateTo = latest);
        var athlete = await CreateAthleteAsync(fx.OrgId, "Hugo", "Paz", latest);
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_InactiveAthlete_IsRejectedRegardlessOfEligibility()
    {
        var fx = await SeedAsync();
        var athlete = await CreateAthleteAsync(fx.OrgId, "Ivan", "Soto", Today.AddYears(-20), isActive: false);
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "AthleteId");
    }

    // ---- Place: one team per person per category ---------------------

    [Fact]
    public async Task InspectAsync_AthleteAlreadyActiveOnAnotherTeamOfTheSameCategory_IsRejected()
    {
        var fx = await SeedAsync();
        var athlete = await CreateAthleteAsync(fx.OrgId, "Julia", "Reyes", Today.AddYears(-20));
        await RegisterAsync(fx.OrgId, fx.TeamAId, athlete.Id);
        await using var session = await OpenAsync(fx, fx.TeamBId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "AthleteId");
    }

    [Fact]
    public async Task InspectAsync_AthleteWithdrawnFromAnotherTeamOfTheSameCategory_IsStillRejected()
    {
        // A withdrawal is a transfer decision somebody has to make, not a gap
        // that frees the player for whoever else fields a team.
        var fx = await SeedAsync();
        var athlete = await CreateAthleteAsync(fx.OrgId, "Karla", "Vidal", Today.AddYears(-20));
        await RegisterAsync(fx.OrgId, fx.TeamAId, athlete.Id, withdrawnAt: DateTimeOffset.UtcNow);
        await using var session = await OpenAsync(fx, fx.TeamBId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "AthleteId");
    }

    [Fact]
    public async Task InspectAsync_AthleteRegisteredInADifferentCategory_DoesNotCollide()
    {
        // The same person, in two different divisions of the same
        // organization, is two different questions.
        var fx = await SeedAsync();
        var otherCategory = await SeedAsync();
        var athlete = await CreateAthleteAsync(fx.OrgId, "Lucas", "Fierro", Today.AddYears(-20));

        // Register the athlete in the *other* fixture's category first.
        await using (var otherSetup = fixture.CreateAppContext())
        await using (var transaction = await otherSetup.Database.BeginTransactionAsync())
        {
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(otherSetup, fx.OrgId);
            otherSetup.RosterEntries.Add(new RosterEntry
            {
                Id = Guid.NewGuid(),
                OrgId = fx.OrgId,
                TeamId = otherCategory.TeamBId,
                AthleteId = athlete.Id,
                RegisteredAt = DateTimeOffset.UtcNow,
            });
            await otherSetup.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_CorrectingTheSameEntry_ExcludesItFromItsOwnCollisionCheck()
    {
        // Re-inspecting an existing registration (to change a jersey number,
        // say) must not have it collide with itself.
        var fx = await SeedAsync();
        var athlete = await CreateAthleteAsync(fx.OrgId, "Mario", "Cano", Today.AddYears(-20));
        var entry = await RegisterAsync(fx.OrgId, fx.TeamAId, athlete.Id);
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, athlete, jerseyNumber: null, excluding: entry.Id, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    // ---- Place: the roster cap -----------------------------------------

    [Fact]
    public async Task InspectAsync_RosterAtCap_RejectsOneMore()
    {
        var fx = await SeedAsync(category => category.MaxRosterSize = 1);
        var already = await CreateAthleteAsync(fx.OrgId, "Nora", "Iban", Today.AddYears(-20));
        await RegisterAsync(fx.OrgId, fx.TeamAId, already.Id);
        var newcomer = await CreateAthleteAsync(fx.OrgId, "Omar", "Leon", Today.AddYears(-20));
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, newcomer, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().ContainSingle(v => v.Property == "AthleteId");
    }

    [Fact]
    public async Task InspectAsync_WithdrawnPlayersDoNotCountAgainstTheCap()
    {
        var fx = await SeedAsync(category => category.MaxRosterSize = 1);
        var left = await CreateAthleteAsync(fx.OrgId, "Pia", "Mora", Today.AddYears(-20));
        await RegisterAsync(fx.OrgId, fx.TeamAId, left.Id, withdrawnAt: DateTimeOffset.UtcNow);
        var newcomer = await CreateAthleteAsync(fx.OrgId, "Quim", "Toro", Today.AddYears(-20));
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, newcomer, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_NoCapDeclared_NeverRejectsForRosterSize()
    {
        var fx = await SeedAsync(); // MaxRosterSize left null

        for (var i = 0; i < 5; i++)
        {
            var player = await CreateAthleteAsync(fx.OrgId, $"Player{i}", "Uncapped", Today.AddYears(-20));
            await RegisterAsync(fx.OrgId, fx.TeamAId, player.Id);
        }

        var oneMore = await CreateAthleteAsync(fx.OrgId, "Rita", "Sola", Today.AddYears(-20));
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, oneMore, jerseyNumber: null, excluding: null, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_PendingRegistrationsFromTheSameBatchCountAgainstTheCap()
    {
        // A bulk import accepts a whole squad in one operation: without
        // this, the row that would put a file over the cap would be told
        // there is room, because the earlier rows of the same file are not
        // in the database yet.
        var fx = await SeedAsync(category => category.MaxRosterSize = 2);
        var candidate = await CreateAthleteAsync(fx.OrgId, "Saul", "Vera", Today.AddYears(-20));
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var violations = await session.Policy.InspectAsync(
            session.Team, session.Category, candidate, jerseyNumber: null, excluding: null, CancellationToken.None, pending: 2);

        violations.Should().ContainSingle(v => v.Property == "AthleteId");
    }

    // ---- Jersey numbers --------------------------------------------------

    [Fact]
    public async Task InspectJerseyAsync_NoNumberRequested_NeverConflicts()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var conflict = await session.Policy.InspectJerseyAsync(session.Team, jerseyNumber: null, excluding: null, CancellationToken.None);

        conflict.Should().BeNull();
    }

    [Fact]
    public async Task InspectJerseyAsync_NumberFree_HasNoConflict()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var conflict = await session.Policy.InspectJerseyAsync(session.Team, jerseyNumber: 7, excluding: null, CancellationToken.None);

        conflict.Should().BeNull();
    }

    [Fact]
    public async Task InspectJerseyAsync_NumberWornByAnActiveTeammate_Conflicts()
    {
        var fx = await SeedAsync();
        var wearer = await CreateAthleteAsync(fx.OrgId, "Tania", "Ruiz", Today.AddYears(-20));
        await RegisterAsync(fx.OrgId, fx.TeamAId, wearer.Id, jerseyNumber: 9);
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var conflict = await session.Policy.InspectJerseyAsync(session.Team, jerseyNumber: 9, excluding: null, CancellationToken.None);

        conflict.Should().NotBeNull();
        conflict!.Property.Should().Be("JerseyNumber");
    }

    [Fact]
    public async Task InspectJerseyAsync_NumberHeldByAWithdrawnPlayer_IsFreeAgain()
    {
        var fx = await SeedAsync();
        var left = await CreateAthleteAsync(fx.OrgId, "Ugo", "Paz", Today.AddYears(-20));
        await RegisterAsync(fx.OrgId, fx.TeamAId, left.Id, jerseyNumber: 9, withdrawnAt: DateTimeOffset.UtcNow);
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var conflict = await session.Policy.InspectJerseyAsync(session.Team, jerseyNumber: 9, excluding: null, CancellationToken.None);

        conflict.Should().BeNull();
    }

    [Fact]
    public async Task InspectJerseyAsync_CorrectingYourOwnEntryToItsCurrentNumber_DoesNotSelfConflict()
    {
        var fx = await SeedAsync();
        var athlete = await CreateAthleteAsync(fx.OrgId, "Vera", "Nino", Today.AddYears(-20));
        var entry = await RegisterAsync(fx.OrgId, fx.TeamAId, athlete.Id, jerseyNumber: 5);
        await using var session = await OpenAsync(fx, fx.TeamAId);

        var conflict = await session.Policy.InspectJerseyAsync(session.Team, jerseyNumber: 5, excluding: entry.Id, CancellationToken.None);

        conflict.Should().BeNull();
    }
}

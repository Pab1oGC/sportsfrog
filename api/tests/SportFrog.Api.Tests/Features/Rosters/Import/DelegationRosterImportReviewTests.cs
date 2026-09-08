using AwesomeAssertions;
using SportFrog.Api.Features.Rosters;
using SportFrog.Api.Features.Rosters.Import;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Rosters.Import;

/// <summary>
/// Says what an uploaded delegation squad sheet would do, without doing any
/// of it. Expected outcomes below are worked out from what the file itself
/// says and from the eligibility rules a category already declares — not
/// from reading DelegationRosterImportReview's private methods and
/// mirroring what they currently return.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class DelegationRosterImportReviewTests(SportFrogDatabaseFixture fixture)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly AnAdult = Today.AddYears(-25);

    private sealed record Fixture(Guid OrgId, Guid CompetitionId, Guid ClubId, Category Open, Category FemaleOnly);

    /// <summary>
    /// One delegation, one competition with two categories: one open to
    /// anybody, one that admits only "F" — enough to prove a row is judged
    /// against the category *it* names, not a single category the whole
    /// file is scoped to.
    /// </summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var openId = Guid.NewGuid();
        var femaleOnlyId = Guid.NewGuid();

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

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Delegación de prueba" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = "taekwondo_kyorugi",
            Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 3, Label = "asalto", Minutes = null },
                Points = new Dictionary<string, int>
                {
                    ["win_2_0"] = 3, ["loss_0_2"] = 0, ["win_2_1"] = 3, ["loss_1_2"] = 1,
                },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId,
            OrgId = orgId,
            SportCode = "taekwondo_kyorugi",
            RulesetId = rulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "knockout",
            Settings = new CompetitionSettings(),
        });

        var open = new Category
        {
            Id = openId,
            OrgId = orgId,
            CompetitionId = competitionId,
            Name = "Abierta -58kg",
        };
        var femaleOnly = new Category
        {
            Id = femaleOnlyId,
            OrgId = orgId,
            CompetitionId = competitionId,
            Name = "Femenino -49kg",
            Gender = "F",
        };
        setup.Categories.Add(open);
        setup.Categories.Add(femaleOnly);

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, competitionId, clubId, open, femaleOnly);
    }

    private async Task<Athlete> CreateAthleteAsync(
        Guid orgId, string firstName, string lastName, string document, string? gender = null)
    {
        var athlete = new Athlete
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            FirstName = firstName,
            LastName = lastName,
            DocumentId = document,
            BirthDate = AnAdult,
            Gender = gender,
        };

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        context.Athletes.Add(athlete);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return athlete;
    }

    /// <summary>Enters an athlete into a category the same way EnrollIndividual would.</summary>
    private async Task EnrollAsync(Guid orgId, Guid clubId, Guid categoryId, Guid athleteId)
    {
        var teamId = Guid.NewGuid();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        context.Teams.Add(new Team
        {
            Id = teamId,
            OrgId = orgId,
            ClubId = clubId,
            CategoryId = categoryId,
            Name = "Ya inscripto",
            IsIndividual = true,
        });
        context.RosterEntries.Add(new RosterEntry
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            TeamId = teamId,
            AthleteId = athleteId,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private (DelegationRosterImportReview Review, SportFrogDbContext Context) OpenReview(Guid orgId)
    {
        var context = fixture.CreateAppContext();

        // RosterPolicy reads through this same context, inside the RLS
        // transaction the caller keeps open for the life of the review.
        return (new DelegationRosterImportReview(context, new RosterPolicy(context)), context);
    }

    private static DelegationSheetRow Row(
        int number, string document, string lastName, string firstName, string categoryName,
        DateOnly? birthDate = null, string? sex = null) =>
        new(number, document, lastName, firstName, birthDate ?? AnAdult, null, sex, categoryName, null, null);

    // ---- The ordinary cases ----------------------------------------------

    [Fact]
    public async Task ReviewAsync_NewAthlete_ValidCategory_IsCreateAndRegister()
    {
        var fx = await SeedAsync();
        var (review, context) = OpenReview(fx.OrgId);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var club = new Club { Id = fx.ClubId, OrgId = fx.OrgId, Name = "x" };
        var rows = new[] { Row(2, "DOC-1", "Rojas", "Ana", fx.Open.Name) };

        var reviewed = await review.ReviewAsync(
            fx.CompetitionId, club, [fx.Open, fx.FemaleOnly], rows, CancellationToken.None);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.CreateAndRegister);
        reviewed.Rows.Single().CategoryId.Should().Be(fx.Open.Id);
    }

    [Fact]
    public async Task ReviewAsync_ExistingAthleteByDocument_NotYetRegisteredInThatCategory_IsRegister()
    {
        var fx = await SeedAsync();
        var existing = await CreateAthleteAsync(fx.OrgId, "Beto", "Gomez", "DOC-2");
        var (review, context) = OpenReview(fx.OrgId);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var club = new Club { Id = fx.ClubId, OrgId = fx.OrgId, Name = "x" };
        var rows = new[] { Row(2, existing.DocumentId, "Gomez", "Beto", fx.Open.Name) };

        var reviewed = await review.ReviewAsync(
            fx.CompetitionId, club, [fx.Open, fx.FemaleOnly], rows, CancellationToken.None);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.Register);
    }

    // ---- Category resolution ---------------------------------------------

    [Fact]
    public async Task ReviewAsync_CategoryNameNotInThisCompetition_IsRejected()
    {
        var fx = await SeedAsync();
        var (review, context) = OpenReview(fx.OrgId);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var club = new Club { Id = fx.ClubId, OrgId = fx.OrgId, Name = "x" };
        var rows = new[] { Row(2, "DOC-3", "Cano", "Mario", "Categoría inventada") };

        var reviewed = await review.ReviewAsync(
            fx.CompetitionId, club, [fx.Open, fx.FemaleOnly], rows, CancellationToken.None);

        var row = reviewed.Rows.Single();
        row.Outcome.Should().Be(ImportOutcome.Rejected);
        row.Problems.Should().ContainSingle(p => p.Contains("Categoría inventada"));
    }

    [Fact]
    public async Task ReviewAsync_CategoryNameMatchedCaseAndAccentInsensitively_IsAccepted()
    {
        // The same normalization RosterSheetReader already uses for headers,
        // reused here for a value instead of a header — a spreadsheet was
        // typed by a person, not copy-pasted from the database.
        var fx = await SeedAsync();
        var (review, context) = OpenReview(fx.OrgId);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var club = new Club { Id = fx.ClubId, OrgId = fx.OrgId, Name = "x" };
        var rows = new[] { Row(2, "DOC-4", "Diaz", "Cami", "  abierta -58KG  ") };

        var reviewed = await review.ReviewAsync(
            fx.CompetitionId, club, [fx.Open, fx.FemaleOnly], rows, CancellationToken.None);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.CreateAndRegister);
    }

    // ---- Cross-category independence --------------------------------------

    [Fact]
    public async Task ReviewAsync_SameDocumentTwoDifferentCategories_BothAccepted()
    {
        // Not a duplicate: the same person entering two different divisions
        // of the same competition is two different questions, exactly as
        // RosterPolicy already treats it category by category.
        var fx = await SeedAsync();
        var (review, context) = OpenReview(fx.OrgId);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var club = new Club { Id = fx.ClubId, OrgId = fx.OrgId, Name = "x" };
        var rows = new[]
        {
            Row(2, "DOC-5", "Fierro", "Lucas", fx.Open.Name),
            Row(3, "DOC-5", "Fierro", "Lucas", fx.FemaleOnly.Name, sex: "F"),
        };

        var reviewed = await review.ReviewAsync(
            fx.CompetitionId, club, [fx.Open, fx.FemaleOnly], rows, CancellationToken.None);

        reviewed.Rows.Should().OnlyContain(row => row.Outcome == ImportOutcome.CreateAndRegister);
    }

    [Fact]
    public async Task ReviewAsync_SameDocumentSameCategoryTwice_SecondRowIsRejected()
    {
        var fx = await SeedAsync();
        var (review, context) = OpenReview(fx.OrgId);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var club = new Club { Id = fx.ClubId, OrgId = fx.OrgId, Name = "x" };
        var rows = new[]
        {
            Row(2, "DOC-6", "Iban", "Nora", fx.Open.Name),
            Row(3, "DOC-6", "Iban", "Nora", fx.Open.Name),
        };

        var reviewed = await review.ReviewAsync(
            fx.CompetitionId, club, [fx.Open, fx.FemaleOnly], rows, CancellationToken.None);

        reviewed.Rows[0].Outcome.Should().Be(ImportOutcome.CreateAndRegister);
        reviewed.Rows[1].Outcome.Should().Be(ImportOutcome.Rejected);
    }

    // ---- Already registered -----------------------------------------------

    [Fact]
    public async Task ReviewAsync_AthleteAlreadyEnteredInThatExactCategory_IsAlreadyRegistered()
    {
        var fx = await SeedAsync();
        var athlete = await CreateAthleteAsync(fx.OrgId, "Omar", "Leon", "DOC-7");
        await EnrollAsync(fx.OrgId, fx.ClubId, fx.Open.Id, athlete.Id);

        var (review, context) = OpenReview(fx.OrgId);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var club = new Club { Id = fx.ClubId, OrgId = fx.OrgId, Name = "x" };
        var rows = new[] { Row(2, athlete.DocumentId, "Leon", "Omar", fx.Open.Name) };

        var reviewed = await review.ReviewAsync(
            fx.CompetitionId, club, [fx.Open, fx.FemaleOnly], rows, CancellationToken.None);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.AlreadyRegistered);
    }

    [Fact]
    public async Task ReviewAsync_AthleteAlreadyEnteredInADifferentCategory_IsStillAcceptedHere()
    {
        var fx = await SeedAsync();
        var athlete = await CreateAthleteAsync(fx.OrgId, "Pia", "Mora", "DOC-8", gender: "F");
        await EnrollAsync(fx.OrgId, fx.ClubId, fx.Open.Id, athlete.Id);

        var (review, context) = OpenReview(fx.OrgId);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var club = new Club { Id = fx.ClubId, OrgId = fx.OrgId, Name = "x" };
        var rows = new[] { Row(2, athlete.DocumentId, "Mora", "Pia", fx.FemaleOnly.Name, sex: "F") };

        var reviewed = await review.ReviewAsync(
            fx.CompetitionId, club, [fx.Open, fx.FemaleOnly], rows, CancellationToken.None);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.Register);
    }

    // ---- Reused eligibility rules ------------------------------------------

    [Fact]
    public async Task ReviewAsync_SexNotAdmittedByTheChosenCategory_IsRejectedByRosterPolicy()
    {
        var fx = await SeedAsync();
        var (review, context) = OpenReview(fx.OrgId);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var club = new Club { Id = fx.ClubId, OrgId = fx.OrgId, Name = "x" };
        var rows = new[] { Row(2, "DOC-9", "Toro", "Quim", fx.FemaleOnly.Name, sex: "M") };

        var reviewed = await review.ReviewAsync(
            fx.CompetitionId, club, [fx.Open, fx.FemaleOnly], rows, CancellationToken.None);

        var row = reviewed.Rows.Single();
        row.Outcome.Should().Be(ImportOutcome.Rejected);
        row.Problems.Should().ContainSingle(p => p.Contains("admite solo"));
    }

    [Fact]
    public async Task ReviewAsync_MissingRequiredField_IsRejectedBeforeAnyEligibilityCheck()
    {
        var fx = await SeedAsync();
        var (review, context) = OpenReview(fx.OrgId);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var club = new Club { Id = fx.ClubId, OrgId = fx.OrgId, Name = "x" };
        var rows = new[] { new DelegationSheetRow(2, null, "Vera", "Saul", AnAdult, null, null, fx.Open.Name, null, null) };

        var reviewed = await review.ReviewAsync(
            fx.CompetitionId, club, [fx.Open, fx.FemaleOnly], rows, CancellationToken.None);

        reviewed.Rows.Single().Outcome.Should().Be(ImportOutcome.Rejected);
    }
}

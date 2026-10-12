using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using SportFrog.Api.Tests.Infrastructure.Validation;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Athletes;

/// <summary>
/// The gender, age and weight filters that used to run in the browser over
/// whatever <c>/athletes</c> already handed it — moved here so a listing
/// that grows past a page still narrows correctly. The age boundary is the
/// one worth pinning exactly: it is counted the same way the panel's own
/// <c>edad()</c> counts a birthday, not by subtracting calendar years, and
/// an off-by-one there would silently place someone in the wrong bracket
/// rather than fail loudly.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ReadAthletesFilterTests(SportFrogDatabaseFixture fixture)
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Held fixed rather than read from the real clock, so the boundary math
    // below means the same thing no matter which day this actually runs.
    private static readonly DateTimeOffset Today = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedTimeProvider(Today);

    private async Task<Guid> SeedOrganizationAsync()
    {
        var orgId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        await setup.SaveChangesAsync();

        return orgId;
    }

    private async Task<Guid> SeedAthleteAsync(
        Guid orgId, string firstName, DateOnly birthDate, string? gender = null, decimal? weightKg = null)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var athlete = new Athlete
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            FirstName = firstName,
            LastName = "Apellido",
            DocumentId = Guid.NewGuid().ToString("N")[..10],
            BirthDate = birthDate,
            Gender = gender,
            WeightKg = weightKg,
        };
        context.Athletes.Add(athlete);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return athlete.Id;
    }

    private async Task<List<ReadAthletes.Summary>> ListAsync(
        Guid orgId,
        string? gender = null,
        int? minAge = null,
        int? maxAge = null,
        decimal? minWeight = null,
        decimal? maxWeight = null)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Viewer, userId: Guid.NewGuid(), ipAddress: null);

        // The store is never reached: every athlete this file seeds has no
        // PhotoKey, and AthletePhoto.LinkAsync returns null for that case
        // before it ever touches the store (see AthletePhoto.cs) — standing
        // up real object storage for a filter that has nothing to do with
        // photographs would test nothing this suite doesn't already.
        var assessor = new PhotoAssessor(
            new UnavailablePhotoValidator(), TimeProvider.System, NullLogger<PhotoAssessor>.Instance);
        var photos = new AthletePhoto(
            null!, organization, assessor, NullLogger<AthletePhoto>.Instance);
        var httpContext = new DefaultHttpContext();

        var result = await ReadAthletes.ListAsync(
            context, photos, httpContext, Clock, CancellationToken.None,
            gender: gender, minAge: minAge, maxAge: maxAge, minWeight: minWeight, maxWeight: maxWeight);

        return Assert.IsType<Ok<List<ReadAthletes.Summary>>>(result).Value!;
    }

    [Fact]
    public async Task ListAsync_AgeRangeAtTheExactYearBoundary_IncludesOnlyThoseTrulyInRange()
    {
        var orgId = await SeedOrganizationAsync();

        // Today is 2026-09-28. Turned 10 exactly today: too old for a 9-only
        // filter.
        await SeedAthleteAsync(orgId, "DiezHoy", new DateOnly(2016, 9, 28));

        // One day younger: still 9, has not turned 10 yet.
        await SeedAthleteAsync(orgId, "NueveJusto", new DateOnly(2016, 9, 29));

        // Turns 9 exactly today: still 9, the youngest edge of the same
        // bracket.
        await SeedAthleteAsync(orgId, "NueveHoy", new DateOnly(2017, 9, 28));

        // One day younger still: turns 9 tomorrow, so today is still 8.
        await SeedAthleteAsync(orgId, "OchoJusto", new DateOnly(2017, 9, 29));

        var listing = await ListAsync(orgId, minAge: 9, maxAge: 9);

        listing.Select(row => row.FirstName).Should().BeEquivalentTo(["NueveJusto", "NueveHoy"]);
    }

    [Fact]
    public async Task ListAsync_OnlyAMinimumAgeGiven_ExcludesEveryoneYounger()
    {
        var orgId = await SeedOrganizationAsync();
        await SeedAthleteAsync(orgId, "Adulto", new DateOnly(1990, 1, 1));
        await SeedAthleteAsync(orgId, "Nino", new DateOnly(2020, 1, 1));

        var listing = await ListAsync(orgId, minAge: 18);

        listing.Select(row => row.FirstName).Should().Equal("Adulto");
    }

    [Fact]
    public async Task ListAsync_WeightRangeGiven_ExcludesAthletesWithNoWeightOnFile()
    {
        var orgId = await SeedOrganizationAsync();
        await SeedAthleteAsync(orgId, "ConPeso", new DateOnly(2000, 1, 1), weightKg: 65m);
        await SeedAthleteAsync(orgId, "SinPeso", new DateOnly(2000, 1, 1), weightKg: null);
        await SeedAthleteAsync(orgId, "FueraDeRango", new DateOnly(2000, 1, 1), weightKg: 120m);

        var listing = await ListAsync(orgId, minWeight: 50m, maxWeight: 80m);

        listing.Select(row => row.FirstName).Should().Equal("ConPeso");
    }

    [Fact]
    public async Task ListAsync_GenderGiven_MatchesOnlyThatGender()
    {
        var orgId = await SeedOrganizationAsync();
        await SeedAthleteAsync(orgId, "Masculino", new DateOnly(2000, 1, 1), gender: "M");
        await SeedAthleteAsync(orgId, "Femenino", new DateOnly(2000, 1, 1), gender: "F");
        await SeedAthleteAsync(orgId, "SinDefinir", new DateOnly(2000, 1, 1), gender: null);

        var listing = await ListAsync(orgId, gender: "M");

        listing.Select(row => row.FirstName).Should().Equal("Masculino");
    }

    [Fact]
    public async Task ListAsync_NoFiltersGiven_ReturnsEveryone()
    {
        var orgId = await SeedOrganizationAsync();
        await SeedAthleteAsync(orgId, "Uno", new DateOnly(2000, 1, 1));
        await SeedAthleteAsync(orgId, "Dos", new DateOnly(2010, 1, 1));

        var listing = await ListAsync(orgId);

        listing.Should().HaveCount(2);
    }
}

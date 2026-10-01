using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>Whether <see cref="AthletesList"/> actually reads the organization's roster — a query that compiles
/// can still fail the moment EF Core tries to translate it, which only shows up against a real database.</summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class AthletesListTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(Guid OrgId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>Three athletes: two share a last name (to exercise search), one of each gender, one inactive.</summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Test User",
        });
        await setup.SaveChangesAsync();

        var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Athletes.Add(new Athlete
        {
            Id = Guid.NewGuid(), OrgId = orgId, FirstName = "Juan", LastName = "Diaz",
            DocumentId = "doc-0001", BirthDate = new DateOnly(2000, 1, 1), Gender = "M",
            WeightKg = 70, IsActive = true,
        });
        setup.Athletes.Add(new Athlete
        {
            Id = Guid.NewGuid(), OrgId = orgId, FirstName = "Ana", LastName = "Diaz",
            DocumentId = "doc-0002", BirthDate = new DateOnly(2001, 2, 2), Gender = "F",
            WeightKg = 60, IsActive = false,
        });
        setup.Athletes.Add(new Athlete
        {
            Id = Guid.NewGuid(), OrgId = orgId, FirstName = "Luis", LastName = "Soto",
            DocumentId = "doc-0003", BirthDate = new DateOnly(1999, 3, 3), Gender = "M",
            WeightKg = null, IsActive = true,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        return new Fixture(orgId);
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    [Fact]
    public async Task LoadAsync_NoFilters_ListsEveryoneOrderedByLastThenFirstName()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AthletesList().LoadAsync(ListScope.Empty, session.Context, CancellationToken.None);

        table!.Title.Should().Be("Deportistas");
        var rows = table.Sections.Single().Rows;
        rows.Should().HaveCount(3);
        rows.Select(row => row[1]).Should().Equal("Diaz, Ana", "Diaz, Juan", "Soto, Luis");
    }

    [Fact]
    public async Task LoadAsync_SearchBySurname_NarrowsToMatchingAthletes()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AthletesList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["search"] = "diaz" }), session.Context, CancellationToken.None);

        table!.Sections.Single().Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_SearchByDocument_AlsoMatches()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AthletesList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["search"] = "0003" }), session.Context, CancellationToken.None);

        table!.Sections.Single().Rows.Should().ContainSingle(row => row[1]!.Equals("Soto, Luis"));
    }

    [Fact]
    public async Task LoadAsync_GenderFilter_NarrowsToThatGender()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AthletesList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["gender"] = "F" }), session.Context, CancellationToken.None);

        table!.Sections.Single().Rows.Should().ContainSingle(row => row[1]!.Equals("Diaz, Ana"));
    }

    [Fact]
    public async Task LoadAsync_NoWeightOnFile_ShowsNullRatherThanZero()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AthletesList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["search"] = "soto" }), session.Context, CancellationToken.None);

        table!.Sections.Single().Rows.Single()[4].Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_InactiveAthlete_CarriesItInTheActiveColumn()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AthletesList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["search"] = "ana" }), session.Context, CancellationToken.None);

        table!.Sections.Single().Rows.Single()[5].Should().Be(false);
    }
}

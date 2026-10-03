using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Lists;

/// <summary>
/// Whether <see cref="ListBranding"/> actually finds the right competition
/// from a category or a team — a query that compiles can still fail the
/// moment EF Core tries to translate it, which only shows up against a real
/// database.
/// </summary>
/// <remarks>
/// None of these ever set a <c>LogoKey</c> — a real one would make
/// <see cref="CompetitionBranding.FromAsync"/> reach into <see cref="ObjectStore"/>
/// for an object nothing here ever wrote, the same "never actually called"
/// reasoning <c>ReportQueriesTests</c> already relies on for its own Store.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ListBrandingTests(SportFrogDatabaseFixture fixture)
{
    private static readonly ObjectStore Store = new(
        null!, Options.Create(new StorageOptions()), NullLogger<ObjectStore>.Instance);

    private sealed record Fixture(Guid OrgId, Guid CategoryId, Guid TeamId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    private async Task<Fixture> SeedAsync(string? accentColor)
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club A" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId, OrgId = orgId, SportCode = "football", Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId, OrgId = orgId, SportCode = "football", RulesetId = rulesetId,
            Name = "Copa de Prueba", Slug = $"copa-{competitionId:N}", Season = "2026",
            Format = CompetitionFormat.League,
            Settings = new CompetitionSettings { Public = new PublicSettings { AccentColor = accentColor } },
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Primera" });
        setup.Teams.Add(new Team { Id = teamId, OrgId = orgId, ClubId = clubId, CategoryId = categoryId, Name = "Equipo A" });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        return new Fixture(orgId, categoryId, teamId);
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    private static ListScope CategoryScope(Guid categoryId) =>
        new(new Dictionary<string, string> { ["categoryId"] = categoryId.ToString() });

    private static ListScope TeamScope(Guid teamId) =>
        new(new Dictionary<string, string> { ["teamId"] = teamId.ToString() });

    [Fact]
    public async Task ResolveAsync_NoCategoryOrTeamInScope_ReturnsNone()
    {
        var fx = await SeedAsync("#ff0000");
        await using var session = await OpenAsync(fx);

        var branding = await ListBranding.ResolveAsync(ListScope.Empty, session.Context, Store, CancellationToken.None);

        branding.Should().Be(CompetitionBranding.None);
    }

    [Fact]
    public async Task ResolveAsync_UnknownCategoryId_ReturnsNone()
    {
        var fx = await SeedAsync("#ff0000");
        await using var session = await OpenAsync(fx);

        var branding = await ListBranding.ResolveAsync(
            CategoryScope(Guid.NewGuid()), session.Context, Store, CancellationToken.None);

        branding.Should().Be(CompetitionBranding.None);
    }

    [Fact]
    public async Task ResolveAsync_KnownCategoryId_ResolvesItsCompetitionsAccentColor()
    {
        var fx = await SeedAsync("#ff0000");
        await using var session = await OpenAsync(fx);

        var branding = await ListBranding.ResolveAsync(
            CategoryScope(fx.CategoryId), session.Context, Store, CancellationToken.None);

        branding.AccentColor.Should().Be("#ff0000");
        branding.LogoBytes.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_KnownTeamId_ResolvesViaItsCategorysCompetition()
    {
        var fx = await SeedAsync("#00ff00");
        await using var session = await OpenAsync(fx);

        var branding = await ListBranding.ResolveAsync(TeamScope(fx.TeamId), session.Context, Store, CancellationToken.None);

        branding.AccentColor.Should().Be("#00ff00");
    }

    [Fact]
    public async Task ResolveAsync_CompetitionNeverSetAnAccentColor_ReadsAsNullRatherThanSomeDefault()
    {
        var fx = await SeedAsync(accentColor: null);
        await using var session = await OpenAsync(fx);

        var branding = await ListBranding.ResolveAsync(
            CategoryScope(fx.CategoryId), session.Context, Store, CancellationToken.None);

        branding.AccentColor.Should().BeNull();
    }
}

using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Accreditation;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Accreditation;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Accreditation;

/// <summary>
/// What one person's accreditation resolves to once their category's package
/// and their own exceptions are merged. Expected results below come from the
/// rule the entities themselves declare — an override grants what the
/// category does not carry, or takes away what it does — not from reading
/// <see cref="AccreditationResolver.ResolveAsync"/> and mirroring whatever it
/// currently returns.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class AccreditationResolverTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(
        Guid OrgId,
        Guid CompetitionId,
        Guid CategoryId,
        Guid FutId,
        Guid VilId,
        Guid AzulId,
        Guid ComId);

    /// <summary>
    /// One RLS-scoped connection held open for the life of one test, the same
    /// reason <c>RosterPolicyTests</c> holds one: every row the resolver
    /// reads has to be seen inside the transaction that carries
    /// <c>app.current_org</c>, which does not survive past it.
    /// </summary>
    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public AccreditationResolver Resolver { get; } = new(context);

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>
    /// One competition with a four-item catalogue — a discipline, a venue, a
    /// coloured zone and a service — and one category ("Aa") whose package is
    /// everything except the service. Every test below grants or denies
    /// against this same fixed package, which is what makes "added by an
    /// override" and "still there despite one" distinguishable.
    /// </summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var futId = Guid.NewGuid();
        var vilId = Guid.NewGuid();
        var azulId = Guid.NewGuid();
        var comId = Guid.NewGuid();

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

        setup.AccreditationItems.AddRange(
            new AccreditationItem
            {
                Id = futId, OrgId = orgId, CompetitionId = competitionId,
                Kind = AccreditationItemKind.Discipline, Code = "FUT", Name = "Fútbol",
            },
            new AccreditationItem
            {
                Id = vilId, OrgId = orgId, CompetitionId = competitionId,
                Kind = AccreditationItemKind.Venue, Code = "VIL", Name = "Villa",
            },
            new AccreditationItem
            {
                Id = azulId, OrgId = orgId, CompetitionId = competitionId,
                Kind = AccreditationItemKind.Zone, Code = "AZUL", Name = "Campo de juego",
                ColorHex = "#1F3864",
            },
            new AccreditationItem
            {
                Id = comId, OrgId = orgId, CompetitionId = competitionId,
                Kind = AccreditationItemKind.Service, Code = "COM", Name = "Comedor",
            });

        setup.AccreditationCategories.Add(new AccreditationCategory
        {
            Id = categoryId, OrgId = orgId, CompetitionId = competitionId,
            Code = "Aa", Name = "Deportista", ColorHex = "#1F3864",
        });

        // The package: everything but the dining service.
        setup.AccreditationCategoryItems.AddRange(
            new AccreditationCategoryItem { OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId, ItemId = futId },
            new AccreditationCategoryItem { OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId, ItemId = vilId },
            new AccreditationCategoryItem { OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId, ItemId = azulId });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, competitionId, categoryId, futId, vilId, azulId, comId);
    }

    private async Task<Guid> AccreditAsync(Fixture fx)
    {
        var athleteId = Guid.NewGuid();
        var accreditationId = Guid.NewGuid();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.Athletes.Add(new Athlete
        {
            Id = athleteId,
            OrgId = fx.OrgId,
            FirstName = "Ana",
            LastName = "Rojas",
            DocumentId = $"DOC-{Guid.NewGuid():N}",
            BirthDate = new DateOnly(2008, 1, 1),
        });

        context.AthleteAccreditations.Add(new AthleteAccreditation
        {
            Id = accreditationId,
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            AthleteId = athleteId,
            CategoryId = fx.CategoryId,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return accreditationId;
    }

    private async Task GrantOrDenyAsync(Fixture fx, Guid accreditationId, Guid itemId, bool granted)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.AthleteAccreditationItems.Add(new AthleteAccreditationItem
        {
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            AccreditationId = accreditationId,
            ItemId = itemId,
            Granted = granted,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    [Fact]
    public async Task ResolveAsync_NoOverrides_ReturnsExactlyTheCategorysPackage()
    {
        var fx = await SeedAsync();
        var accreditationId = await AccreditAsync(fx);
        await using var session = await OpenAsync(fx);

        var resolved = await session.Resolver.ResolveAsync(accreditationId, CancellationToken.None);

        resolved.Select(item => item.ItemId).Should().BeEquivalentTo([fx.FutId, fx.VilId, fx.AzulId]);
        resolved.Should().AllSatisfy(item => item.Source.Should().Be(AccreditationItemSource.Category));
    }

    [Fact]
    public async Task ResolveAsync_OverrideDeniesAPackageItem_DropsExactlyThatItem()
    {
        var fx = await SeedAsync();
        var accreditationId = await AccreditAsync(fx);
        await GrantOrDenyAsync(fx, accreditationId, fx.VilId, granted: false);
        await using var session = await OpenAsync(fx);

        var resolved = await session.Resolver.ResolveAsync(accreditationId, CancellationToken.None);

        resolved.Select(item => item.ItemId).Should().BeEquivalentTo([fx.FutId, fx.AzulId]);
    }

    [Fact]
    public async Task ResolveAsync_OverrideGrantsAnItemOutsideThePackage_AddsItWithOverrideSource()
    {
        var fx = await SeedAsync();
        var accreditationId = await AccreditAsync(fx);
        await GrantOrDenyAsync(fx, accreditationId, fx.ComId, granted: true);
        await using var session = await OpenAsync(fx);

        var resolved = await session.Resolver.ResolveAsync(accreditationId, CancellationToken.None);

        resolved.Select(item => item.ItemId).Should().BeEquivalentTo([fx.FutId, fx.VilId, fx.AzulId, fx.ComId]);
        resolved.Single(item => item.ItemId == fx.ComId).Source.Should().Be(AccreditationItemSource.Override);
        resolved.Where(item => item.ItemId != fx.ComId)
            .Should().AllSatisfy(item => item.Source.Should().Be(AccreditationItemSource.Category));
    }

    [Fact]
    public async Task ResolveAsync_OneOverrideDeniesAndAnotherGrants_BothApplyIndependently()
    {
        var fx = await SeedAsync();
        var accreditationId = await AccreditAsync(fx);
        await GrantOrDenyAsync(fx, accreditationId, fx.FutId, granted: false);
        await GrantOrDenyAsync(fx, accreditationId, fx.ComId, granted: true);
        await using var session = await OpenAsync(fx);

        var resolved = await session.Resolver.ResolveAsync(accreditationId, CancellationToken.None);

        resolved.Select(item => item.ItemId).Should().BeEquivalentTo([fx.VilId, fx.AzulId, fx.ComId]);
    }

    [Fact]
    public async Task ResolveAsync_ZoneCarriesAColour_ColourSurvivesResolution()
    {
        var fx = await SeedAsync();
        var accreditationId = await AccreditAsync(fx);
        await using var session = await OpenAsync(fx);

        var resolved = await session.Resolver.ResolveAsync(accreditationId, CancellationToken.None);

        resolved.Single(item => item.ItemId == fx.AzulId).ColorHex.Should().Be("#1F3864");
    }

    [Fact]
    public async Task ResolveAsync_AccreditationDoesNotExist_ReturnsEmpty()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var resolved = await session.Resolver.ResolveAsync(Guid.NewGuid(), CancellationToken.None);

        resolved.Should().BeEmpty();
    }

    // ---- ResolveManyAsync: the batch path a credential job actually calls ----

    [Fact]
    public async Task ResolveManyAsync_SeveralAccreditations_ResolvesEachOneIndependently()
    {
        var fx = await SeedAsync();
        var withNoOverrides = await AccreditAsync(fx);
        var withAnOverride = await AccreditAsync(fx);
        await GrantOrDenyAsync(fx, withAnOverride, fx.VilId, granted: false);
        await using var session = await OpenAsync(fx);

        var resolved = await session.Resolver.ResolveManyAsync(
            [withNoOverrides, withAnOverride], CancellationToken.None);

        resolved.Keys.Should().BeEquivalentTo([withNoOverrides, withAnOverride]);
        resolved[withNoOverrides].Keys.Should().BeEquivalentTo([fx.FutId, fx.VilId, fx.AzulId]);
        resolved[withAnOverride].Keys.Should().BeEquivalentTo([fx.FutId, fx.AzulId]);
    }

    [Fact]
    public async Task ResolveManyAsync_SameItemGrantedDifferentlyPerPerson_EachKeepsItsOwnSource()
    {
        var fx = await SeedAsync();
        var fromCategory = await AccreditAsync(fx);
        var fromOverride = await AccreditAsync(fx);
        await GrantOrDenyAsync(fx, fromCategory, fx.ComId, granted: false); // not in the package; a no-op deny
        await GrantOrDenyAsync(fx, fromOverride, fx.ComId, granted: true);
        await using var session = await OpenAsync(fx);

        var resolved = await session.Resolver.ResolveManyAsync(
            [fromCategory, fromOverride], CancellationToken.None);

        resolved[fromCategory].Should().NotContainKey(fx.ComId);
        resolved[fromOverride][fx.ComId].Should().Be(AccreditationItemSource.Override);
    }

    [Fact]
    public async Task ResolveManyAsync_NoIdsGiven_ReturnsAnEmptyDictionaryWithoutQuerying()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var resolved = await session.Resolver.ResolveManyAsync([], CancellationToken.None);

        resolved.Should().BeEmpty();
    }
}

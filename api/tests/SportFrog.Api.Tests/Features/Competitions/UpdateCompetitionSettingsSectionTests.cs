using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Infrastructure.Caching;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Tests.Features.Competitions;

/// <summary>
/// The handlers behind <c>PATCH /competitions/{id}/settings/bulletin</c> and
/// <c>PATCH /competitions/{id}/settings/public</c>: that each corrects only
/// the one settings section it is for, leaving the rest — the ruleset, the
/// other settings section — exactly as it was.
/// </summary>
/// <remarks>
/// The picture store is passed as null, the same choice
/// <c>CompetitionNameEndpointTests</c> makes for <c>UpdateCompetition</c>:
/// every <see cref="PublicSettings"/> used here carries no picture field, so
/// <c>CompetitionPortalPictures.ResolveAsync</c> never reaches it.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class UpdateCompetitionSettingsSectionTests(SportFrogDatabaseFixture fixture)
{
    private static readonly IPublicQueryCache PublicCache =
        new PublicQueryCache(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task Bulletin_CorrectsOnlyTheBulletinSection()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2026");
        competition.Settings = new CompetitionSettings
        {
            Public = new PublicSettings { Description = "Vieja descripción." },
            Bulletin = new BulletinSettings { Introduction = "Vieja presentación." },
        };
        scope.Context.Competitions.Add(competition);
        await scope.Context.SaveChangesAsync();

        var result = await UpdateCompetitionBulletinSettings.HandleAsync(
            competition.Id,
            new BulletinSettings { Introduction = "Nueva presentación.", Sanctions = "Dos amarillas, un partido." },
            scope.Context,
            CancellationToken.None);

        Assert.IsType<NoContent>(result);

        var stored = await scope.Context.Competitions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == competition.Id);

        Assert.Equal("Nueva presentación.", stored.Settings.Bulletin?.Introduction);
        Assert.Equal("Dos amarillas, un partido.", stored.Settings.Bulletin?.Sanctions);
        // The public section was never named by this request — it has to
        // read back exactly as it was saved, not cleared by a PATCH that
        // only ever carried a BulletinSettings body.
        Assert.Equal("Vieja descripción.", stored.Settings.Public?.Description);
    }

    [Fact]
    public async Task Bulletin_WithNull_ClearsTheSection()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2026");
        competition.Settings = new CompetitionSettings
        {
            Bulletin = new BulletinSettings { Introduction = "Se va a borrar." },
        };
        scope.Context.Competitions.Add(competition);
        await scope.Context.SaveChangesAsync();

        var result = await UpdateCompetitionBulletinSettings.HandleAsync(
            competition.Id, null, scope.Context, CancellationToken.None);

        Assert.IsType<NoContent>(result);

        var stored = await scope.Context.Competitions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == competition.Id);

        Assert.Null(stored.Settings.Bulletin);
    }

    [Fact]
    public async Task Bulletin_UnknownCompetition_IsNotFound()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var result = await UpdateCompetitionBulletinSettings.HandleAsync(
            Guid.NewGuid(), new BulletinSettings(), scope.Context, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Public_CorrectsOnlyThePublicSection()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2026");
        competition.Settings = new CompetitionSettings
        {
            Bulletin = new BulletinSettings { Introduction = "No la toca este PATCH." },
            Public = new PublicSettings { ShowRosters = false },
        };
        scope.Context.Competitions.Add(competition);
        await scope.Context.SaveChangesAsync();

        var result = await UpdateCompetitionPublicSettings.HandleAsync(
            competition.Id,
            new PublicSettings { ShowRosters = true, Description = "Torneo abierto." },
            scope.Context,
            null!,
            PublicCache,
            CancellationToken.None);

        Assert.IsType<NoContent>(result);

        var stored = await scope.Context.Competitions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == competition.Id);

        Assert.True(stored.Settings.Public?.ShowRosters);
        Assert.Equal("Torneo abierto.", stored.Settings.Public?.Description);
        // The bulletin section was never named by this request either.
        Assert.Equal("No la toca este PATCH.", stored.Settings.Bulletin?.Introduction);
    }

    [Fact]
    public async Task Public_WithNull_ClearsTheSection()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2026");
        competition.Settings = new CompetitionSettings
        {
            Public = new PublicSettings { Description = "Se va a borrar." },
        };
        scope.Context.Competitions.Add(competition);
        await scope.Context.SaveChangesAsync();

        var result = await UpdateCompetitionPublicSettings.HandleAsync(
            competition.Id, null, scope.Context, null!, PublicCache, CancellationToken.None);

        Assert.IsType<NoContent>(result);

        var stored = await scope.Context.Competitions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == competition.Id);

        Assert.Null(stored.Settings.Public);
    }

    [Fact]
    public async Task Public_UnknownCompetition_IsNotFound()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var result = await UpdateCompetitionPublicSettings.HandleAsync(
            Guid.NewGuid(), new PublicSettings(), scope.Context, null!, PublicCache, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }
}

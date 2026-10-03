using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
/// What <see cref="ReadLists"/> itself is responsible for: resolving a slug,
/// reading the query string into a <see cref="ListScope"/>, and enforcing
/// the row cap — the three things no provider and no renderer already
/// covers. Stub providers stand in throughout, so these never depend on what
/// any real list actually contains.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ReadListsTests(SportFrogDatabaseFixture fixture)
{
    private sealed class StubListProvider(
        string slug, Func<ListScope, ListTable?> load, IReadOnlyList<ListParameter>? parameters = null,
        Func<ScoreMode, bool>? appliesTo = null)
        : IListProvider
    {
        public ListScope? ReceivedScope { get; private set; }

        public string Slug { get; } = slug;

        public string Label => $"Lista {Slug}";

        public IReadOnlyList<ListParameter> Parameters { get; } = parameters ?? [];

        public Task<bool> AppliesToAsync(
            string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken) =>
            Task.FromResult((appliesTo ?? (_ => true))(scoreMode));

        public Task<ListTable?> LoadAsync(ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken)
        {
            ReceivedScope = scope;
            return Task.FromResult(load(scope));
        }
    }

    // None of these stub providers' scopes ever resolve to a real category
    // or team, so ListBranding.ResolveAsync always falls through to
    // CompetitionBranding.None without ever reaching this store's own S3
    // client — the same reasoning ReportQueriesTests already relies on for
    // its own "never actually called" Store.
    private static readonly ObjectStore Store = new(
        null!, Options.Create(new StorageOptions()), NullLogger<ObjectStore>.Instance);

    private static ListTable SampleTable(int rows = 1) => new(
        "Lista de prueba", null,
        [new ListColumn("Nombre", ListValueKind.Text)],
        [new ListSection(null, [.. Enumerable.Range(0, rows).Select(i => (IReadOnlyList<object?>)[$"Fila {i}"])])]);

    private static DefaultHttpContext HttpContextWith(string queryString = "") =>
        new() { Request = { QueryString = new QueryString(queryString) } };

    private SportFrogDbContext Database() => fixture.CreateAppContext();

    [Fact]
    public async Task Catalog_ListsEveryRegisteredProviderWithItsOwnParameters()
    {
        var parameters = new ListParameter[] { new("categoryId", ListParameterKind.Category, true) };
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => null, parameters)]);
        await using var database = Database();

        var entries = (await ReadLists.Catalog(registry, database, CancellationToken.None))
            .Should().BeOfType<Ok<List<ReadLists.CatalogEntry>>>().Subject.Value!;

        entries.Should().ContainSingle();
        entries[0].Slug.Should().Be("goleadores");
        entries[0].Label.Should().Be("Lista goleadores");
        entries[0].Parameters.Should().BeEquivalentTo(parameters);
    }

    [Fact]
    public async Task Catalog_NoCompetitionIdGiven_AppliesNoScoreModeFilter()
    {
        var registry = new ListRegistry([
            new StubListProvider("solo-juzgado", _ => null, appliesTo: mode => mode == ScoreMode.Judged),
            new StubListProvider("nunca-juzgado", _ => null, appliesTo: mode => mode != ScoreMode.Judged),
        ]);
        await using var database = Database();

        var entries = (await ReadLists.Catalog(registry, database, CancellationToken.None))
            .Should().BeOfType<Ok<List<ReadLists.CatalogEntry>>>().Subject.Value!;

        entries.Select(entry => entry.Slug).Should().BeEquivalentTo(["solo-juzgado", "nunca-juzgado"]);
    }

    [Fact]
    public async Task Catalog_CompetitionIdGiven_OnlyIncludesProvidersApplicableToItsSport()
    {
        var orgId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();

        await using (var setup = fixture.CreateAppContext())
        {
            setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
            await setup.SaveChangesAsync();

            var transaction = await setup.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

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

            await setup.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var registry = new ListRegistry([
            new StubListProvider("solo-juzgado", _ => null, appliesTo: mode => mode == ScoreMode.Judged),
            new StubListProvider("nunca-juzgado", _ => null, appliesTo: mode => mode != ScoreMode.Judged),
            new StubListProvider("siempre", _ => null, appliesTo: _ => true),
        ]);

        // Reading the competition back needs its organization's own context
        // set, the same RLS policy every other org-scoped read in this
        // feature is already subject to — ListBrandingTests relies on the
        // same thing for the branding it reads through a competition.
        await using var database = fixture.CreateAppContext();
        var readTransaction = await database.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(database, orgId);

        var entries = (await ReadLists.Catalog(registry, database, CancellationToken.None, competitionId))
            .Should().BeOfType<Ok<List<ReadLists.CatalogEntry>>>().Subject.Value!;

        entries.Select(entry => entry.Slug).Should().BeEquivalentTo(["solo-juzgado", "siempre"]);

        await readTransaction.DisposeAsync();
    }

    [Fact]
    public async Task Catalog_UnknownCompetitionId_AppliesNoScoreModeFilterRatherThanHidingEverything()
    {
        var registry = new ListRegistry([
            new StubListProvider("solo-juzgado", _ => null, appliesTo: mode => mode == ScoreMode.Judged),
            new StubListProvider("nunca-juzgado", _ => null, appliesTo: mode => mode != ScoreMode.Judged),
        ]);
        await using var database = Database();

        var entries = (await ReadLists.Catalog(registry, database, CancellationToken.None, Guid.NewGuid()))
            .Should().BeOfType<Ok<List<ReadLists.CatalogEntry>>>().Subject.Value!;

        entries.Select(entry => entry.Slug).Should().BeEquivalentTo(["solo-juzgado", "nunca-juzgado"]);
    }

    [Fact]
    public async Task PreviewAsync_UnregisteredSlug_ReturnsNotFound()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => SampleTable())]);
        await using var database = Database();

        var result = await ReadLists.PreviewAsync(
            "tarjetas", HttpContextWith(), registry, database, CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task PreviewAsync_ProviderFindsNothingForTheScope_ReturnsNotFound()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => null)]);
        await using var database = Database();

        var result = await ReadLists.PreviewAsync(
            "goleadores", HttpContextWith(), registry, database, CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task PreviewAsync_KnownSlug_ReturnsTheTable()
    {
        var table = SampleTable();
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => table)]);
        await using var database = Database();

        var result = await ReadLists.PreviewAsync(
            "goleadores", HttpContextWith(), registry, database, CancellationToken.None);

        result.Should().BeOfType<Ok<ListTable>>().Which.Value.Should().BeSameAs(table);
    }

    [Fact]
    public async Task PreviewAsync_ReadsTheQueryStringIntoTheScopeHandedToTheProvider()
    {
        StubListProvider? provider = null;
        provider = new StubListProvider("goleadores", scope => scope.GetGuid("categoryId") is { } ? SampleTable() : null);
        var registry = new ListRegistry([provider]);
        await using var database = Database();
        var categoryId = Guid.NewGuid();

        await ReadLists.PreviewAsync(
            "goleadores", HttpContextWith($"?categoryId={categoryId}"), registry, database, CancellationToken.None);

        provider.ReceivedScope!.GetGuid("categoryId").Should().Be(categoryId);
    }

    [Fact]
    public async Task ExportXlsxAsync_UnregisteredSlug_ReturnsNotFound()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => SampleTable())]);
        await using var database = Database();

        var result = await ReadLists.ExportXlsxAsync(
            "tarjetas", HttpContextWith(), registry, database, Store, CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task ExportXlsxAsync_KnownSlug_ReturnsAnExcelFileNamedAfterTheTitle()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => SampleTable())]);
        await using var database = Database();

        var result = await ReadLists.ExportXlsxAsync(
            "goleadores", HttpContextWith(), registry, database, Store, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentHttpResult>().Subject;
        file.ContentType.Should().Be(ListXlsx.MimeType);
        file.FileDownloadName.Should().Be("lista-de-prueba.xlsx");
        file.FileContents.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportPdfAsync_KnownSlug_ReturnsAPdfFileNamedAfterTheTitle()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => SampleTable())]);
        await using var database = Database();

        var result = await ReadLists.ExportPdfAsync(
            "goleadores", HttpContextWith(), registry, database, Store, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentHttpResult>().Subject;
        file.ContentType.Should().Be(ListPdf.MimeType);
        file.FileDownloadName.Should().Be("lista-de-prueba.pdf");
    }

    [Fact]
    public async Task ExportXlsxAsync_ProviderFindsNothingForTheScope_ReturnsNotFound()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => null)]);
        await using var database = Database();

        var result = await ReadLists.ExportXlsxAsync(
            "goleadores", HttpContextWith(), registry, database, Store, CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task ExportXlsxAsync_MoreRowsThanTheCap_ReturnsAValidationProblemRatherThanTheFile()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => SampleTable(rows: 10_001))]);
        await using var database = Database();

        var result = await ReadLists.ExportXlsxAsync(
            "goleadores", HttpContextWith(), registry, database, Store, CancellationToken.None);

        result.Should().BeOfType<ProblemHttpResult>();
    }
}

using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Tests.Infrastructure.Persistence;

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
        string slug, Func<ListScope, ListTable?> load, IReadOnlyList<ListParameter>? parameters = null)
        : IListProvider
    {
        public ListScope? ReceivedScope { get; private set; }

        public string Slug { get; } = slug;

        public string Label => $"Lista {Slug}";

        public IReadOnlyList<ListParameter> Parameters { get; } = parameters ?? [];

        public Task<ListTable?> LoadAsync(ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken)
        {
            ReceivedScope = scope;
            return Task.FromResult(load(scope));
        }
    }

    private static ListTable SampleTable(int rows = 1) => new(
        "Lista de prueba", null,
        [new ListColumn("Nombre", ListValueKind.Text)],
        [new ListSection(null, [.. Enumerable.Range(0, rows).Select(i => (IReadOnlyList<object?>)[$"Fila {i}"])])]);

    private static DefaultHttpContext HttpContextWith(string queryString = "") =>
        new() { Request = { QueryString = new QueryString(queryString) } };

    private SportFrogDbContext Database() => fixture.CreateAppContext();

    [Fact]
    public void Catalog_ListsEveryRegisteredProviderWithItsOwnParameters()
    {
        var parameters = new ListParameter[] { new("categoryId", ListParameterKind.Category, true) };
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => null, parameters)]);

        var entries = ReadLists.Catalog(registry).Should().BeOfType<Ok<List<ReadLists.CatalogEntry>>>().Subject.Value!;

        entries.Should().ContainSingle();
        entries[0].Slug.Should().Be("goleadores");
        entries[0].Label.Should().Be("Lista goleadores");
        entries[0].Parameters.Should().BeEquivalentTo(parameters);
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
            "tarjetas", HttpContextWith(), registry, database, CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task ExportXlsxAsync_KnownSlug_ReturnsAnExcelFileNamedAfterTheTitle()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => SampleTable())]);
        await using var database = Database();

        var result = await ReadLists.ExportXlsxAsync(
            "goleadores", HttpContextWith(), registry, database, CancellationToken.None);

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
            "goleadores", HttpContextWith(), registry, database, CancellationToken.None);

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
            "goleadores", HttpContextWith(), registry, database, CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task ExportXlsxAsync_MoreRowsThanTheCap_ReturnsAValidationProblemRatherThanTheFile()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores", _ => SampleTable(rows: 10_001))]);
        await using var database = Database();

        var result = await ReadLists.ExportXlsxAsync(
            "goleadores", HttpContextWith(), registry, database, CancellationToken.None);

        result.Should().BeOfType<ProblemHttpResult>();
    }
}

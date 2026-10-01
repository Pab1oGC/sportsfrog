using System.Text;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Lists;

/// <summary>
/// The one door every exportable list is read and downloaded through: a
/// catalog, a JSON preview, and the two files <see cref="ListXlsx"/> and
/// <see cref="ListPdf"/> know how to render.
/// </summary>
/// <remarks>
/// Every other feature's endpoint reads its own named route and query
/// parameters, because it already knows what it needs. This one cannot: the
/// slug names which <see cref="IListProvider"/> runs, and only that provider
/// knows which parameters it takes. So the query string is read whole, as a
/// <see cref="ListScope"/>, and handed to whichever provider the slug
/// resolves to — the one place in this feature a request's own inputs are
/// read generically instead of bound by name.
/// </remarks>
public static class ReadLists
{
    /// <summary>
    /// Past this many rows, an export risks building the whole file in
    /// memory at once for no reader who could use a sheet that long. Every
    /// list this project ships today scopes to one category or one team,
    /// which keeps rows in the hundreds — this exists for the day a provider
    /// scopes to a whole organization instead.
    /// </summary>
    private const int MaxExportableRows = 10_000;

    public sealed record CatalogEntry(string Slug, string Label, IReadOnlyList<ListParameter> Parameters);

    public static IEndpointRouteBuilder MapReadLists(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/lists", Catalog)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadLists))
            .WithSummary("Catalogs every exportable list and the inputs each one needs.");

        routes.MapGet("/lists/{slug}", PreviewAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadLists) + "Preview")
            .WithSummary("Reads one list's rows for an on-screen preview.");

        routes.MapGet("/lists/{slug}.xlsx", ExportXlsxAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadLists) + "Xlsx")
            .WithSummary("Exports one list as an Excel workbook.");

        routes.MapGet("/lists/{slug}.pdf", ExportPdfAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadLists) + "Pdf")
            .WithSummary("Exports one list as a PDF document.");

        return routes;
    }

    // internal, not private: testable directly against a real database, the
    // same convention Athletes.ReadAthletes.ListAsync already uses.
    internal static IResult Catalog(IListRegistry registry) =>
        Results.Ok(registry.All
            .Select(provider => new CatalogEntry(provider.Slug, provider.Label, provider.Parameters))
            .ToList());

    internal static async Task<IResult> PreviewAsync(
        string slug,
        HttpContext httpContext,
        IListRegistry registry,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var (table, error) = await ResolveTableAsync(slug, httpContext, registry, database, cancellationToken);

        return error ?? Results.Ok(table);
    }

    internal static async Task<IResult> ExportXlsxAsync(
        string slug,
        HttpContext httpContext,
        IListRegistry registry,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var (table, error) = await ResolveTableAsync(slug, httpContext, registry, database, cancellationToken);

        return error ?? Results.File(ListXlsx.Render(table!), ListXlsx.MimeType, FileName(table!, "xlsx"));
    }

    internal static async Task<IResult> ExportPdfAsync(
        string slug,
        HttpContext httpContext,
        IListRegistry registry,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var (table, error) = await ResolveTableAsync(slug, httpContext, registry, database, cancellationToken);

        return error ?? Results.File(ListPdf.Render(table!), ListPdf.MimeType, FileName(table!, "pdf"));
    }

    /// <summary>
    /// Resolves the slug, loads the table, and enforces the row cap — the one
    /// place all three handlers above agree on what counts as "no such list"
    /// or "too big to hand back", so a slug typo or an oversized scope
    /// answers the same way whether it was asked for as JSON, Excel, or PDF.
    /// </summary>
    private static async Task<(ListTable? Table, IResult? Error)> ResolveTableAsync(
        string slug,
        HttpContext httpContext,
        IListRegistry registry,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (!registry.TryFor(slug, out var provider))
        {
            return (null, Results.NotFound());
        }

        var table = await provider.LoadAsync(ScopeFrom(httpContext), database, cancellationToken);

        if (table is null)
        {
            return (null, Results.NotFound());
        }

        var rowCount = table.Sections.Sum(section => section.Rows.Count);

        if (rowCount > MaxExportableRows)
        {
            return (null, Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["scope"] =
                [
                    $"Esta lista tiene {rowCount:N0} filas. Filtrá por categoría, equipo u otro criterio " +
                    $"más específico para traerla (el máximo son {MaxExportableRows:N0}).",
                ],
            }));
        }

        return (table, null);
    }

    private static ListScope ScopeFrom(HttpContext httpContext) =>
        new(httpContext.Request.Query.ToDictionary(
            pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal));

    /// <summary>
    /// Named after the list's own title rather than its slug — the same
    /// normalization <c>Competitions.Bulletin.ReadCompetitionBulletin.FileName</c>
    /// already applies to a competition's name.
    /// </summary>
    private static string FileName(ListTable table, string extension)
    {
        var readable = string.Concat(table.Title
                .Normalize(NormalizationForm.FormD)
                .Where(character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '-'))
            .Trim()
            .Replace(' ', '-')
            .ToLowerInvariant();

        return $"{(readable.Length > 0 ? readable : "lista")}.{extension}";
    }
}

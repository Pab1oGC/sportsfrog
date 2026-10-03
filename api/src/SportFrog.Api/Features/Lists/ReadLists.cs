using System.Text;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;

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

    /// <param name="competitionId">
    /// Narrows the catalog to the lists that have anything to say for this
    /// competition's own sport — see <see cref="IListProvider.AppliesToAsync"/>.
    /// Omitted, the catalog reads the same as it always has: every list this
    /// feature registers, for a caller that has not chosen a competition yet
    /// or never needs one (the one list this feature registers that is
    /// organization-wide, not competition-scoped, still shows up either way).
    /// </param>
    // internal, not private: testable directly against a real database, the
    // same convention Athletes.ReadAthletes.ListAsync already uses.
    internal static async Task<IResult> Catalog(
        IListRegistry registry, SportFrogDbContext database, CancellationToken cancellationToken, Guid? competitionId = null)
    {
        var sport = competitionId is { } id
            ? await ResolveSportAsync(database, id, cancellationToken)
            : null;

        IEnumerable<IListProvider> providers = registry.All;

        if (sport is { SportCode: var sportCode, ScoreMode: var scoreMode })
        {
            var applicable = new List<IListProvider>();

            foreach (var provider in registry.All)
            {
                if (await provider.AppliesToAsync(sportCode, scoreMode, database, cancellationToken))
                {
                    applicable.Add(provider);
                }
            }

            providers = applicable;
        }

        return Results.Ok(providers
            .Select(provider => new CatalogEntry(provider.Slug, provider.Label, provider.Parameters))
            .ToList());
    }

    /// <summary>
    /// A competition's own sport code and score mode, read the same way
    /// <c>ClassificationList</c> and <c>ChampionResolver</c> already read it
    /// — or null when the id names no competition at all, which filters
    /// nothing rather than hiding every list behind a typo in a query string.
    /// </summary>
    private static async Task<(string SportCode, ScoreMode ScoreMode)?> ResolveSportAsync(
        SportFrogDbContext database, Guid competitionId, CancellationToken cancellationToken)
    {
        var sportCode = await database.Competitions
            .AsNoTracking()
            .Where(competition => competition.Id == competitionId)
            .Select(competition => (string?)competition.SportCode)
            .SingleOrDefaultAsync(cancellationToken);

        if (sportCode is null)
        {
            return null;
        }

        var scoreMode = await database.Sports
            .AsNoTracking()
            .Where(sport => sport.Code == sportCode)
            .Select(sport => (ScoreMode?)sport.ScoreMode)
            .SingleOrDefaultAsync(cancellationToken);

        return scoreMode is { } mode ? (sportCode, mode) : null;
    }

    internal static async Task<IResult> PreviewAsync(
        string slug,
        HttpContext httpContext,
        IListRegistry registry,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var (table, error) = await ResolveTableAsync(slug, ScopeFrom(httpContext), registry, database, cancellationToken);

        return error ?? Results.Ok(table);
    }

    internal static async Task<IResult> ExportXlsxAsync(
        string slug,
        HttpContext httpContext,
        IListRegistry registry,
        SportFrogDbContext database,
        ObjectStore store,
        CancellationToken cancellationToken)
    {
        var scope = ScopeFrom(httpContext);
        var (table, error) = await ResolveTableAsync(slug, scope, registry, database, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var branding = await ListBranding.ResolveAsync(scope, database, store, cancellationToken);

        return Results.File(ListXlsx.Render(table!, branding), ListXlsx.MimeType, FileName(table!, "xlsx"));
    }

    internal static async Task<IResult> ExportPdfAsync(
        string slug,
        HttpContext httpContext,
        IListRegistry registry,
        SportFrogDbContext database,
        ObjectStore store,
        CancellationToken cancellationToken)
    {
        var scope = ScopeFrom(httpContext);
        var (table, error) = await ResolveTableAsync(slug, scope, registry, database, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var branding = await ListBranding.ResolveAsync(scope, database, store, cancellationToken);

        return Results.File(ListPdf.Render(table!, branding), ListPdf.MimeType, FileName(table!, "pdf"));
    }

    /// <summary>
    /// Resolves the slug, loads the table, and enforces the row cap — the one
    /// place all three handlers above agree on what counts as "no such list"
    /// or "too big to hand back", so a slug typo or an oversized scope
    /// answers the same way whether it was asked for as JSON, Excel, or PDF.
    /// </summary>
    private static async Task<(ListTable? Table, IResult? Error)> ResolveTableAsync(
        string slug,
        ListScope scope,
        IListRegistry registry,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (!registry.TryFor(slug, out var provider))
        {
            return (null, Results.NotFound());
        }

        var table = await provider.LoadAsync(scope, database, cancellationToken);

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

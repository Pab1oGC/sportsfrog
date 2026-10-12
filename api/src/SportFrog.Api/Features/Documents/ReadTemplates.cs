using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// The designs an organization has, and what one of them looks like.
/// </summary>
/// <remarks>
/// A read hands back the layout with the artwork as a link the browser can
/// load rather than as the key the column holds. The editor draws the card
/// from that link; the key never leaves this API in a form anybody has to
/// understand.
/// </remarks>
public static class ReadTemplates
{
    public sealed record Summary(
        Guid Id,
        DocumentKind Kind,
        string Name,
        string PageSize,
        bool IsDefault,
        int Version,
        DateTimeOffset UpdatedAt);

    /// <summary>
    /// Temporary links to the artwork, one per face.
    /// </summary>
    /// <remarks>
    /// Alongside the layout rather than inside it, and that is not a
    /// presentation choice. A design editor reads a template, moves something
    /// and sends it back; if the read had folded a link into each face, what
    /// came back would carry a property the layout does not have — and since
    /// unknown properties are refused, the editor could open a design it could
    /// never save. The layout in this response is exactly the document that
    /// goes back in a PUT.
    ///
    /// They expire. They are for the page that asked for them, not for storing.
    /// </remarks>
    public sealed record Backgrounds(string? Front, string? Back);

    public sealed record Detail(
        Guid Id,
        DocumentKind Kind,
        string Name,
        string PageSize,
        bool IsDefault,
        int Version,
        TemplateLayout Layout,
        Backgrounds Backgrounds,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public static IEndpointRouteBuilder MapReadTemplates(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/documents/templates", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadTemplates))
            .WithSummary("Lists the document designs of the active organization.");

        routes.MapGet("/documents/templates/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadTemplate")
            .WithSummary("Reads one design, with its artwork.");

        routes.MapGet("/documents/templates/{id:guid}/versions/{version:int}", ReadVersionAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadTemplateVersion")
            .WithSummary("Reads a design as it was at one version.");

        return routes;
    }

    /// <summary>
    /// The listing carries no layout at all.
    /// </summary>
    /// <remarks>
    /// A design is dozens of fields and two signed links; a page showing ten
    /// of them would move all of it to display ten names. The editor asks for
    /// the one it is about to open.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        string? kind = null)
    {
        var wanted = QueryFilter.OrAbsent(kind);
        DocumentKind? only = null;

        if (wanted is not null)
        {
            if (!WireEnum.TryParse<DocumentKind>(wanted, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["kind"] =
                        [$"Tipo desconocido. Disponibles: {WireEnum.Options<DocumentKind>()}."],
                });
            }

            only = parsed;
        }

        return Results.Ok(await database.Set<DocumentTemplate>()
            .AsNoTracking()
            // Credentials were designed here before they had their own; those
            // old rows are not listed, since nothing prints from them any more.
            .Where(template => template.Kind == DocumentKind.Certificate)
            .Where(template => only == null || template.Kind == only)
            .OrderBy(template => template.Kind)
            .ThenByDescending(template => template.IsDefault)
            .ThenBy(template => template.Name)
            .Select(template => new Summary(
                template.Id,
                template.Kind,
                template.Name,
                template.PageSize,
                template.IsDefault,
                template.Version,
                template.UpdatedAt))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        TemplateBackground backgrounds,
        CancellationToken cancellationToken)
    {
        var template = await database.Set<DocumentTemplate>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return template is null
            ? Results.NotFound()
            : Results.Ok(new Detail(
                template.Id,
                template.Kind,
                template.Name,
                template.PageSize,
                template.IsDefault,
                template.Version,
                template.Layout,
                await LinkAsync(template.Layout, backgrounds, cancellationToken),
                template.CreatedAt,
                template.UpdatedAt));
    }

    /// <summary>
    /// A design as it was, which is what a reissue prints from.
    /// </summary>
    /// <remarks>
    /// The reason the archive exists, made readable: somebody holding a
    /// credential from last season can be shown the design it was printed
    /// under rather than the one that replaced it.
    /// </remarks>
    private static async Task<IResult> ReadVersionAsync(
        Guid id,
        int version,
        SportFrogDbContext database,
        TemplateBackground backgrounds,
        CancellationToken cancellationToken)
    {
        var stored = await database.Set<DocumentTemplateVersion>()
            .AsNoTracking()

            // Reaches a retired design on purpose. This endpoint answers "what
            // did the card printed under version 3 look like", and the answer
            // does not stop existing because the league stopped using that
            // design. Isolation is untouched: the rows are still the
            // organization's own, enforced by the database.
            .IgnoreQueryFilters()
            .Include(candidate => candidate.Template)
            .SingleOrDefaultAsync(
                candidate => candidate.TemplateId == id && candidate.Version == version,
                cancellationToken);

        return stored?.Template is not { } template
            ? Results.NotFound()
            : Results.Ok(new Detail(
                template.Id,
                template.Kind,
                template.Name,
                stored.PageSize,
                template.IsDefault,
                stored.Version,
                stored.Layout,
                await LinkAsync(stored.Layout, backgrounds, cancellationToken),
                stored.CreatedAt,
                stored.CreatedAt));
    }

    /// <summary>Turns the stored keys into links somebody can load.</summary>
    private static async Task<Backgrounds> LinkAsync(
        TemplateLayout layout,
        TemplateBackground backgrounds,
        CancellationToken cancellationToken) =>
        new(
            await backgrounds.LinkAsync(layout.Front.BackgroundKey, cancellationToken),
            layout.Back is { } back
                ? await backgrounds.LinkAsync(back.BackgroundKey, cancellationToken)
                : null);
}

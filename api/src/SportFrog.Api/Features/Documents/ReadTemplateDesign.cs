using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Everything a design may be made of.
/// </summary>
/// <remarks>
/// The layout editor is a page in a browser and the generator is this
/// process, and the two have to agree about every list in here. Published
/// rather than duplicated: a front-end holding its own copy of the fields
/// would eventually offer one the generator cannot print, and the discovery
/// would be four hundred credentials with a blank space where the club's name
/// was meant to go.
///
/// Filtered by kind, because what a credential can carry and what a
/// certificate can carry are not the same set — a certificate has no business
/// printing an identity document, and a credential has no reason to print the
/// motive of an award.
/// </remarks>
public static class ReadTemplateDesign
{
    public sealed record Source(string Code, string Label, string Shape);

    public sealed record Font(string Code, string Label, string Family);

    public sealed record Page(string Code, string Label, double WidthMm, double HeightMm);

    public sealed record Defaults(string Font, string Align, string Fit);

    public sealed record Response(
        IReadOnlyList<Source> Sources,
        IReadOnlyList<Font> Fonts,
        IReadOnlyList<Page> PageSizes,
        IReadOnlyList<string> Alignments,
        IReadOnlyList<string> Fits,
        Defaults Defaults);

    public static IEndpointRouteBuilder MapReadTemplateDesign(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/documents/design", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadTemplateDesign))
            .WithSummary("Lists the fields, typefaces and sizes a document design may use.");

        return routes;
    }

    private static IResult HandleAsync(string? kind = null)
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

        var sources = TemplateDesign.Sources
            .Where(source => only is not { } wantedKind || source.Kinds.Contains(wantedKind))
            .Select(source => new Source(
                source.Code, source.Label, source.Shape.ToString().ToLowerInvariant()))
            .ToList();

        return Results.Ok(new Response(
            sources,
            [.. TemplateDesign.Fonts.Select(font => new Font(font.Code, font.Label, font.Family))],
            [.. TemplateDesign.PageSizes.Select(size =>
                new Page(size.Code, size.Label, size.Width, size.Height))],
            TemplateDesign.Alignments,
            TemplateDesign.Fits,
            new Defaults(
                TemplateDesign.DefaultFont,
                TemplateDesign.DefaultAlign,
                TemplateDesign.DefaultFit)));
    }
}

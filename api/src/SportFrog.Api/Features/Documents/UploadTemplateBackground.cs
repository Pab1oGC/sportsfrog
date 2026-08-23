using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Takes the artwork a design will be laid out over.
/// </summary>
/// <remarks>
/// Separate from saving a template, because it happens first and on its own:
/// a designer uploads the card, sees it on screen, drags fields onto it, and
/// only then saves anything. The key comes back here and travels into the
/// layout when the design is saved.
///
/// It leaves an object behind if the design is then abandoned. That is the
/// right trade — the alternative is refusing to show anybody their artwork
/// until they have committed to a template — and the cost is a few unused
/// images, not a broken reference.
/// </remarks>
public static class UploadTemplateBackground
{
    /// <summary>
    /// Artwork, not a photograph. A card designed in a print program and
    /// exported at 300 dots per inch is a few megabytes, and refusing that
    /// would refuse the file every designer actually has.
    /// </summary>
    private const long MaximumUpload = 20 * 1024 * 1024;

    public sealed record Response(string BackgroundKey, string BackgroundUrl, double AspectRatio);

    public static IEndpointRouteBuilder MapUploadTemplateBackground(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/documents/templates/backgrounds", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .DisableAntiforgery()
            .WithName(nameof(UploadTemplateBackground))
            .WithSummary("Stores the artwork a document design is laid out over.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        IFormFile file,
        TemplateBackground backgrounds,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Refuse("El archivo está vacío.");
        }

        if (file.Length > MaximumUpload)
        {
            return Refuse($"La imagen pesa más de {MaximumUpload / (1024 * 1024)} MB.");
        }

        using var contents = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await contents.CopyToAsync(buffer, cancellationToken);

        if (await backgrounds.StoreAsync(buffer.ToArray(), cancellationToken) is not { } artwork)
        {
            return Refuse("No se pudo leer el archivo como imagen.");
        }

        // The shape comes back with the key so the editor does not have to
        // measure the image itself, and so the proportion that goes into the
        // layout is the proportion of what was actually stored.
        return Results.Ok(new Response(
            artwork.Key,
            await backgrounds.LinkAsync(artwork.Key, cancellationToken) ?? string.Empty,
            artwork.AspectRatio));
    }

    private static IResult Refuse(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["file"] = [message],
        });
}

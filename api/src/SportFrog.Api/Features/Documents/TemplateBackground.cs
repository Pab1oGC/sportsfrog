using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Keeps the artwork a design is laid out over.
/// </summary>
/// <remarks>
/// The same arrangement as an athlete's photograph, for the same reasons: the
/// row holds a key, the reader gets a link that expires, and the image is
/// normalized on the way in so what is stored is one predictable thing.
///
/// The ceiling is higher than a portrait's, because this is artwork that gets
/// printed at card size and read at arm's length — a badge printed at 300 dpi
/// wants more pixels across 85 millimetres than a face does across 25.
/// </remarks>
public sealed class TemplateBackground(ObjectStore store, OrganizationContext organization)
{
    /// <summary>Stored artwork: where it is, and what shape it came out.</summary>
    public sealed record Stored(string Key, double AspectRatio);

    /// <summary>
    /// Stores uploaded artwork, or nothing if it is not an image.
    /// </summary>
    /// <remarks>
    /// The proportion is measured on what was stored rather than on what was
    /// uploaded, and the two differ more often than they sound like they
    /// would: an image with a rotation recorded in its metadata comes out of
    /// normalization with its edges swapped.
    /// </remarks>
    public async Task<Stored?> StoreAsync(byte[] uploaded, CancellationToken cancellationToken)
    {
        if (ImageNormalizer.Normalize(uploaded, Artwork) is not { } background)
        {
            return null;
        }

        var organizationId = organization.RequireOrganizationId();
        var key = StorageKeys.TemplateBackground(organizationId, background.Extension);

        await store.PutAsync(key, background.Content, background.ContentType, cancellationToken);

        return new Stored(
            key, Math.Round((double)background.Width / background.Height, 4));
    }

    /// <summary>A temporary link the editor can draw the artwork from.</summary>
    public async Task<string?> LinkAsync(string? key, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(key)
            ? null
            : await store.ReadLinkAsync(
                organization.RequireOrganizationId(), key, cancellationToken);

    /// <summary>
    /// Wide enough to print a card at 300 dots per inch with room to spare.
    /// </summary>
    private const int Artwork = 2400;
}

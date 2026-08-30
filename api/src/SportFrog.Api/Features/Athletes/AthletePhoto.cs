using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Athletes;

/// <summary>
/// Keeps a registered person's photograph, and hands it back out.
/// </summary>
/// <remarks>
/// The row stores a key into object storage; the request and the response
/// both speak in pictures. Somebody creating an athlete sends a data URL, the
/// way a form does, and reads back a link they can put in an img tag. That
/// neither of those is what the column contains is this file's business and
/// nobody else's — which is why the conversion lives here instead of at the
/// four call sites that would each have had to remember it.
///
/// The photograph is a minor's, more often than not. It is stored under the
/// organization's prefix, handed out only through a link that expires, and
/// never reaches the public view (RNF-16).
/// </remarks>
public sealed class AthletePhoto(ObjectStore store, OrganizationContext organization)
{
    /// <summary>
    /// Normalizes and stores an uploaded photograph, answering the key it was
    /// stored under — or null if the payload was not an image after all.
    /// </summary>
    /// <remarks>
    /// The distinction matters: the validator has already checked that the
    /// request announced an image and is small enough, and both of those are
    /// claims made by whoever sent it. Nothing has decoded a single byte
    /// until here.
    /// </remarks>
    public async Task<string?> StoreAsync(
        Guid athleteId,
        string dataUrl,
        CancellationToken cancellationToken) =>
        InlinePhoto.TryRead(dataUrl, out var uploaded)
            ? await StoreAsync(athleteId, uploaded, cancellationToken)
            : null;

    /// <summary>
    /// The same, for a photograph that did not arrive as a data URL.
    /// </summary>
    /// <remarks>
    /// A batch of images out of an archive is bytes already. Sharing the rest
    /// of the path matters more than it looks: the normalization is where the
    /// picture is turned upright and where the camera's record of where it
    /// was taken is removed, and a second way in that skipped it would
    /// publish four hundred children's locations rather than one.
    /// </remarks>
    public async Task<string?> StoreAsync(
        Guid athleteId,
        byte[] uploaded,
        CancellationToken cancellationToken)
    {
        if (ImageNormalizer.Normalize(uploaded) is not { } photo)
        {
            return null;
        }

        var organizationId = organization.RequireOrganizationId();
        var key = StorageKeys.AthletePhoto(organizationId, athleteId, photo.Extension);

        await store.PutAsync(key, photo.Content, photo.ContentType, cancellationToken);

        return key;
    }

    /// <summary>
    /// A link the browser can load, for a stored photograph.
    /// </summary>
    /// <remarks>
    /// A value that is already a data URL is handed back untouched. Those are
    /// the photographs uploaded before this module existed, when the image
    /// itself lived in the column: they are still somebody's picture, and
    /// refusing to show them because of where they happen to be kept would
    /// destroy data to tidy up an implementation detail. They convert
    /// themselves the next time the athlete is corrected.
    /// </remarks>
    public async Task<string?> LinkAsync(string? stored, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        if (stored.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return stored;
        }

        return await store.ReadLinkAsync(
            organization.RequireOrganizationId(), stored, cancellationToken);
    }

    /// <summary>
    /// Drops a photograph that nothing points at any more.
    /// </summary>
    /// <remarks>
    /// Called after the row has already been pointed at the replacement, so
    /// the worst outcome of a failure is a few kilobytes nobody can reach.
    /// </remarks>
    public async Task ForgetAsync(string? stored, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(stored)
            || stored.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await store.ForgetAsync(
            organization.RequireOrganizationId(), stored, cancellationToken);
    }

    /// <summary>What to say when the bytes turn out not to be a picture.</summary>
    public static IResult NotAnImage() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["photoUrl"] = ["La foto no se pudo leer como una imagen."],
        });
}

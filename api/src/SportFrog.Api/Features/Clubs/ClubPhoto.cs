using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Clubs;

/// <summary>
/// Keeps a club's crest, and hands it back out.
/// </summary>
/// <remarks>
/// Same shape as <see cref="Athletes.AthletePhoto"/>, deliberately: the row
/// stores a key into object storage, the request and the response both speak
/// in pictures, and a caller sends a data URL and reads back a temporary
/// link. A club's crest is not RNF-16 material — it is meant to be shown, on
/// the public portal and on a printed bracket — but it is still a picture an
/// organizer uploads, not a URL they type in, and it deserves the same
/// care: turned upright, stripped of anything a camera recorded about where
/// it was taken, and never left as an arbitrary column of unvalidated text.
///
/// One thing about it is not shared with a photograph, though: a crest is
/// usually artwork on a see-through background, not a rectangle, and
/// normalizing it the way a photo is normalized would flatten that
/// background to opaque white. <see cref="ImageNormalizer"/> is asked to
/// keep the transparency here instead.
/// </remarks>
public sealed class ClubPhoto(ObjectStore store, OrganizationContext organization)
{
    /// <summary>
    /// Normalizes and stores an uploaded crest, answering the key it was
    /// stored under — or null if the payload was not an image after all.
    /// </summary>
    public async Task<string?> StoreAsync(
        Guid clubId,
        string dataUrl,
        CancellationToken cancellationToken)
    {
        // A crest, not a photograph: preserveTransparency keeps it stored as
        // WebP with whatever see-through background the upload had, instead
        // of the JPEG-on-white every other picture here gets — a badge that
        // is round in the artwork should not gain a white square behind it.
        if (!InlinePhoto.TryRead(dataUrl, out var uploaded)
            || ImageNormalizer.Normalize(uploaded, preserveTransparency: true) is not { } photo)
        {
            return null;
        }

        var organizationId = organization.RequireOrganizationId();
        var key = StorageKeys.ClubLogo(organizationId, clubId, photo.Extension);

        await store.PutAsync(key, photo.Content, photo.ContentType, cancellationToken);

        return key;
    }

    /// <summary>A link the browser can load, for a stored crest.</summary>
    public async Task<string?> LinkAsync(string? stored, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        return await store.ReadLinkAsync(
            organization.RequireOrganizationId(), stored, cancellationToken);
    }

    /// <summary>Drops a crest that nothing points at any more.</summary>
    public async Task ForgetAsync(string? stored, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(stored))
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
            ["logoUrl"] = ["El logo no se pudo leer como una imagen."],
        });
}

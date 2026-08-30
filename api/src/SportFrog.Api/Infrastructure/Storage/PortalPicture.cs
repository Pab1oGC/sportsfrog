using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Infrastructure.Storage;

/// <summary>
/// Uploaded pictures that belong to a page rather than to a row — an
/// organization's mark, a competition's cover, a sponsor's logo.
/// </summary>
/// <remarks>
/// Same arrangement as <see cref="Features.Athletes.AthletePhoto"/> and
/// <see cref="Features.Clubs.ClubPhoto"/>: a data URL comes in, a key into
/// object storage is what gets kept, and a caller reads a temporary link back
/// out. Generic across categories rather than one class per kind of picture,
/// because none of them differ in anything but where they are filed — the
/// difference that matters is the <c>category</c> argument, not a second
/// implementation of normalizing and signing an image.
/// </remarks>
public sealed class PortalPicture(ObjectStore store, OrganizationContext organization)
{
    /// <summary>
    /// Normalizes and stores an uploaded picture, answering the key it was
    /// stored under — or null if the payload was not an image after all.
    /// </summary>
    public async Task<string?> StoreAsync(
        string category,
        string dataUrl,
        CancellationToken cancellationToken)
    {
        if (!InlinePhoto.TryRead(dataUrl, out var uploaded)
            || ImageNormalizer.Normalize(uploaded) is not { } photo)
        {
            return null;
        }

        var organizationId = organization.RequireOrganizationId();
        var key = StorageKeys.Picture(organizationId, category, photo.Extension);

        await store.PutAsync(key, photo.Content, photo.ContentType, cancellationToken);

        return key;
    }

    /// <summary>A link the browser can load, for a stored picture.</summary>
    public Task<string?> LinkAsync(string? stored, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(stored)
            ? Task.FromResult<string?>(null)
            : store.ReadLinkAsync(organization.RequireOrganizationId(), stored, cancellationToken);

    /// <summary>Drops a picture that nothing points at any more.</summary>
    public Task ForgetAsync(string? stored, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(stored)
            ? Task.CompletedTask
            : store.ForgetAsync(organization.RequireOrganizationId(), stored, cancellationToken);

    /// <summary>
    /// Whether a value already looks like a key this store produced, as
    /// opposed to a data URL still waiting to be uploaded.
    /// </summary>
    /// <remarks>
    /// A sponsor list is saved whole every time — the ones a caller did not
    /// touch come back exactly as this store handed them out, and the ones
    /// they picked a new picture for come back as data URLs. This is how the
    /// two are told apart without asking the caller to say which is which.
    /// </remarks>
    public static bool IsStoredKey(string value) =>
        !value.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
}

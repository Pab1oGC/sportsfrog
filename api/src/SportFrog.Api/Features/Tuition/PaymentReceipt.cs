using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Tuition;

/// <summary>
/// Keeps a payment's proof, and hands it back out.
/// </summary>
/// <remarks>
/// Same shape as <see cref="Clubs.ClubPhoto"/>: the row stores a key into
/// object storage, the request sends a data URL and the response hands
/// back a temporary link. It does not go through <see cref="Athletes.PhotoAssessor"/>
/// — that validator is calibrated for faces, and a receipt is never one.
///
/// A PDF passes straight through, unnormalized: <see cref="ImageNormalizer"/>
/// only knows how to re-encode a decoded bitmap, and a bank's own export is
/// already exactly the document it needs to stay. An image gets the same
/// three things every other upload here gets — turned upright, stripped of
/// its camera metadata, re-encoded — but kept noticeably larger than a
/// face: the credential profile's 1000-pixel edge would make a transaction
/// number unreadable.
/// </remarks>
public sealed class PaymentReceipt(ObjectStore store, OrganizationContext organization)
{
    /// <summary>
    /// Longest edge kept for a receipt that arrived as a photograph.
    /// </summary>
    /// <remarks>
    /// <see cref="ImageNormalizer.MaximumEdge"/> is sized for a face printed
    /// at credential size; a photograph of a receipt is read by a person
    /// zooming in on a screen to check a transaction number, which needs
    /// noticeably more detail to still be legible.
    /// </remarks>
    private const int MaximumEdge = 1800;

    private const string PdfContentType = "application/pdf";

    private const string PdfExtension = "pdf";

    /// <summary>
    /// Stores an uploaded receipt, answering the key it was stored under —
    /// or null if the payload was neither an acceptable image nor a PDF.
    /// </summary>
    public async Task<string?> StoreAsync(
        Guid chargeId,
        string dataUrl,
        CancellationToken cancellationToken)
    {
        if (!InlineReceipt.TryRead(dataUrl, out var uploaded))
        {
            return null;
        }

        var organizationId = organization.RequireOrganizationId();

        if (InlineReceipt.IsPdf(dataUrl))
        {
            var key = StorageKeys.PaymentReceipt(organizationId, chargeId, PdfExtension);
            await store.PutAsync(key, uploaded, PdfContentType, cancellationToken);

            return key;
        }

        if (ImageNormalizer.Normalize(uploaded, maximumEdge: MaximumEdge) is not { } photo)
        {
            return null;
        }

        var imageKey = StorageKeys.PaymentReceipt(organizationId, chargeId, photo.Extension);
        await store.PutAsync(imageKey, photo.Content, photo.ContentType, cancellationToken);

        return imageKey;
    }

    /// <summary>A link the browser can load, for a stored receipt.</summary>
    public async Task<string?> LinkAsync(string? stored, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        return await store.ReadLinkAsync(
            organization.RequireOrganizationId(), stored, cancellationToken);
    }

    /// <summary>What to say when the bytes turn out not to be an acceptable receipt.</summary>
    public static IResult NotAReceipt() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["receiptDataUrl"] = [InlineReceipt.Requirement],
        });
}

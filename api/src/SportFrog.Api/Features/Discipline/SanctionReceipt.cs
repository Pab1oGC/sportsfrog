using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// Keeps a fine payment's proof, and hands it back out.
/// </summary>
/// <remarks>
/// A sibling of <c>Features.Tuition.PaymentReceipt</c>, not a reuse of it:
/// the two take different ids to file a receipt under, and sharing the type
/// would mean every call here reading as "chargeId" for something that is
/// never a charge. The storage key itself is shared — <see cref="StorageKeys.PaymentReceipt"/>
/// only ever asked for a bucket to file under, never for what the id names —
/// so the two kinds of receipt live apart in the object store the same way
/// they live apart in the database, under one unsurprising helper either
/// way.
/// </remarks>
public sealed class SanctionReceipt(ObjectStore store, OrganizationContext organization)
{
    /// <summary>Longest edge kept for a receipt that arrived as a photograph — see <c>Features.Tuition.PaymentReceipt</c>'s own remarks for why this is larger than a credential photo.</summary>
    private const int MaximumEdge = 1800;

    private const string PdfContentType = "application/pdf";

    private const string PdfExtension = "pdf";

    /// <summary>
    /// Stores an uploaded receipt, answering the key it was stored under —
    /// or null if the payload was neither an acceptable image nor a PDF.
    /// </summary>
    public async Task<string?> StoreAsync(
        Guid sanctionId,
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
            var key = StorageKeys.PaymentReceipt(organizationId, sanctionId, PdfExtension);
            await store.PutAsync(key, uploaded, PdfContentType, cancellationToken);

            return key;
        }

        if (ImageNormalizer.Normalize(uploaded, maximumEdge: MaximumEdge) is not { } photo)
        {
            return null;
        }

        var imageKey = StorageKeys.PaymentReceipt(organizationId, sanctionId, photo.Extension);
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

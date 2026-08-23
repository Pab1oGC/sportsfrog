namespace SportFrog.Api.Infrastructure.Storage;

/// <summary>
/// Where an object lives in the bucket.
/// </summary>
/// <remarks>
/// Every key begins with the organization that owns it. That prefix is what
/// makes isolation checkable in object storage, which has no row-level
/// security to fall back on: a bucket is one flat namespace, and the only
/// thing standing between two organizations' files is the discipline of the
/// code that names them. Building keys here rather than at each call site
/// means the discipline is in one file, and <see cref="Belongs"/> can be
/// asserted wherever a key crosses back out.
///
/// Names carry no meaning. A key is a fresh identifier, not the athlete's
/// document or the uploaded file name: object keys leak into signed links,
/// and a link that reads .../12345678.jpg has published an identity document
/// to whoever it was forwarded to. A random name also sidesteps caching —
/// replacing a photograph writes a new key, so no browser is left showing the
/// previous one, and no cache has to be persuaded to forget.
/// </remarks>
internal static class StorageKeys
{
    /// <summary>Everything belonging to one organization.</summary>
    public static string Prefix(Guid organizationId) => $"orgs/{organizationId:n}/";

    /// <summary>A normalized photograph of a registered person.</summary>
    public static string AthletePhoto(Guid organizationId, Guid athleteId, string extension) =>
        $"{Prefix(organizationId)}athletes/{athleteId:n}/{Guid.NewGuid():n}.{extension}";

    /// <summary>
    /// An uploaded archive of photographs, kept under its batch.
    /// </summary>
    /// <remarks>
    /// Named after the batch rather than after the file somebody uploaded,
    /// because two people sending fotos.zip on the same afternoon is not an
    /// unusual thing to happen and the second one must not land on the first.
    /// </remarks>
    public static string PhotoArchive(Guid organizationId, Guid batchId) =>
        $"{Prefix(organizationId)}photo-imports/{batchId:n}/archive.zip";

    /// <summary>
    /// Artwork a document design is laid out over.
    /// </summary>
    /// <remarks>
    /// Not filed under a template, because it is uploaded before there is one:
    /// a designer picks the artwork, lays fields over it and only then saves
    /// anything. The key travels back with the upload and into the layout.
    /// </remarks>
    public static string TemplateBackground(Guid organizationId, string extension) =>
        $"{Prefix(organizationId)}document-templates/{Guid.NewGuid():n}.{extension}";

    /// <summary>
    /// Whether a key is one of this organization's.
    /// </summary>
    /// <remarks>
    /// Asserted where a stored key is turned into a signed link or into a
    /// deletion — the two places where a key stops being an opaque string and
    /// starts granting access. Under normal operation it is always true,
    /// because keys are read from rows the database already filtered by
    /// organization. It exists for the day that stops being true.
    /// </remarks>
    public static bool Belongs(Guid organizationId, string key) =>
        key.StartsWith(Prefix(organizationId), StringComparison.Ordinal);
}

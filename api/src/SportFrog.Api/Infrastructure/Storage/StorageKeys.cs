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

    /// <summary>A club's crest or logo.</summary>
    public static string ClubLogo(Guid organizationId, Guid clubId, string extension) =>
        $"{Prefix(organizationId)}clubs/{clubId:n}/{Guid.NewGuid():n}.{extension}";

    /// <summary>
    /// An image with no entity of its own to be filed under — the
    /// organization's mark, a competition's cover, a sponsor's logo.
    /// </summary>
    /// <param name="category">
    /// A folder name, not a type check: nothing here validates it against a
    /// fixed list, and the caller is the one who has to keep it stable — a
    /// category that changes name orphans every key already stored under it.
    /// </param>
    public static string Picture(Guid organizationId, string category, string extension) =>
        $"{Prefix(organizationId)}pictures/{category}/{Guid.NewGuid():n}.{extension}";

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

    /// <summary>One issued document's own PDF.</summary>
    /// <remarks>
    /// Filed under the document rather than under the batch that printed it,
    /// because the document outlives the batch: it is sent to one person, it
    /// is reprinted years later, and it stays valid long after nobody
    /// remembers which run produced it.
    /// </remarks>
    public static string IssuedDocument(Guid organizationId, Guid documentId) =>
        $"{Prefix(organizationId)}documents/{documentId:n}.pdf";

    /// <summary>The imposed sheet a batch produced, which is what gets printed.</summary>
    public static string DocumentSheet(Guid organizationId, Guid batchId) =>
        $"{Prefix(organizationId)}document-batches/{batchId:n}/sheet.pdf";

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

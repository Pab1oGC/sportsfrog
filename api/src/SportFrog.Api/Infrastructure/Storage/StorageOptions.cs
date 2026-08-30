using System.ComponentModel.DataAnnotations;

namespace SportFrog.Api.Infrastructure.Storage;

/// <summary>
/// Where objects are kept, and under whose credentials.
/// </summary>
/// <remarks>
/// The access key and the secret are secrets and never live in the
/// repository: they arrive from the environment, like the connection strings
/// and the signing key. Startup fails without them rather than deferring the
/// failure to the first photograph somebody uploads, which would be found by
/// a person filling in a form rather than by whoever deployed it.
///
/// The endpoint is configurable because this speaks to MinIO in development
/// and to an S3-compatible service in production, and the only difference
/// between the two is an address and whether the bucket is addressed as a
/// path or as a subdomain.
/// </remarks>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required(AllowEmptyStrings = false)]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// The address a browser uses to reach the same objects <see cref="Endpoint"/>
    /// serves, when it isn't the same address. Null means it is — a local
    /// container, or a deployment where the storage host is itself public.
    /// </summary>
    /// <remarks>
    /// Behind a reverse proxy that puts object storage on the same public
    /// origin as the site (see web/nginx.conf) but reaches it internally
    /// under a different host — a Docker service name nothing outside the
    /// network can resolve — <see cref="Endpoint"/> still has to be that
    /// internal address: it is what actually receives every request this
    /// process makes, bootstrap check included, and it is what the signature
    /// on a link is computed against. This is only ever used to rewrite the
    /// scheme and host of an already-signed link before it is handed to a
    /// browser — see <see cref="ObjectStore.ReadLinkAsync"/> — never to talk
    /// to storage directly. Changing it can't invalidate a signature that
    /// was already computed.
    /// </remarks>
    public string? PublicUrl { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string Bucket { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string AccessKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether the bucket is addressed as <c>host/bucket</c> rather than as
    /// <c>bucket.host</c>. MinIO needs it; a virtual-hosted deployment does
    /// not.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Signing region. MinIO ignores it but the signature is computed over
    /// it, so both ends have to agree on a value even when it means nothing.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// How long a link handed to a browser stays valid.
    /// </summary>
    /// <remarks>
    /// Short on purpose. A signed link is a bearer credential for one object:
    /// whoever holds it can read that object without signing in, so it is
    /// pasteable, forwardable and, once it reaches a chat log, permanent. Long
    /// enough to load a page and keep it open, not long enough to be worth
    /// sharing. Requirement RNF-16 — most of these objects are photographs of
    /// children.
    /// </remarks>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan ReadLinkLifetime { get; set; } = TimeSpan.FromMinutes(15);
}

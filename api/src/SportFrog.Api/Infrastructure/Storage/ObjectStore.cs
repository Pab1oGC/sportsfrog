using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace SportFrog.Api.Infrastructure.Storage;

/// <summary>
/// Puts objects in the bucket, hands out links to read them, and forgets them.
/// </summary>
/// <remarks>
/// Files do not live in Postgres. A photograph is a megabyte that no query
/// ever filters on, and keeping it in a column means every backup, every
/// replica and every incautious <c>SELECT *</c> carries it. The row keeps the
/// key; the bytes live where bytes belong.
///
/// The organization is an argument on the two operations that grant access,
/// rather than something this reads from the ambient request. Ambient state
/// is exactly what is missing when a background job runs a batch of ten
/// thousand documents with nobody signed in, and a check that quietly reads
/// as "no organization, therefore allow" is worse than no check.
/// </remarks>
public sealed class ObjectStore(
    IAmazonS3 client,
    IOptions<StorageOptions> options,
    ILogger<ObjectStore> logger)
{
    private readonly StorageOptions _options = options.Value;

    /// <summary>Writes an object, replacing whatever was under that key.</summary>
    public async Task PutAsync(
        string key,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var payload = new MemoryStream(content, writable: false);

        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = payload,
                ContentType = contentType,

                // Without this the SDK streams from a length it cannot infer
                // for every stream type, and MinIO answers a chunked upload
                // it did not expect with a signature mismatch.
                AutoCloseStream = false,
            },
            cancellationToken);
    }

    /// <summary>Reads an object back into memory.</summary>
    /// <remarks>
    /// Used by the paths that have to look inside a file rather than hand it
    /// to somebody — a workbook to parse, a background image to draw. A
    /// browser never comes through here; it gets a signed link and fetches
    /// the object itself.
    /// </remarks>
    public async Task<byte[]> ReadAsync(
        Guid organizationId,
        string key,
        CancellationToken cancellationToken)
    {
        Require(organizationId, key);

        using var stored = await client.GetObjectAsync(
            new GetObjectRequest { BucketName = _options.Bucket, Key = key },
            cancellationToken);

        using var buffer = new MemoryStream();
        await stored.ResponseStream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }

    /// <summary>
    /// A temporary link a browser can follow without signing in.
    /// </summary>
    /// <remarks>
    /// Signed rather than proxied through this API: a page showing two
    /// hundred people is two hundred images, and routing them through here
    /// would make every one of them an authenticated request that no browser
    /// cache can help with. The signature is computed locally — no call
    /// leaves the process — so producing a link per row of a listing costs
    /// nothing beyond the arithmetic.
    /// </remarks>
    public async Task<string> ReadLinkAsync(
        Guid organizationId,
        string key,
        CancellationToken cancellationToken)
    {
        Require(organizationId, key);

        return await client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(_options.ReadLinkLifetime),

            // Taken from the endpoint rather than left to the default, which
            // is HTTPS regardless of where the service actually is. A signed
            // link to https://localhost:9000 is signed correctly and refuses
            // to connect, and the browser reports it as a broken image with
            // nothing in any log to explain why.
            Protocol = _scheme,
        });
    }

    /// <summary>
    /// Whether the configured endpoint is plain HTTP.
    /// </summary>
    /// <remarks>
    /// A local container is; a deployment is not. Read once from the address
    /// rather than configured separately, so the two cannot be set to
    /// disagree.
    /// </remarks>
    private readonly Protocol _scheme =
        options.Value.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? Protocol.HTTP
            : Protocol.HTTPS;

    /// <summary>
    /// Removes an object, and does not make a failure the caller's problem.
    /// </summary>
    /// <remarks>
    /// Deliberately best effort. This is called after a replacement has
    /// already been written and the row already points at it: the old bytes
    /// are unreachable through the application whether or not the bucket
    /// obliges. Failing the request here would refuse a correction somebody
    /// legitimately made because of housekeeping they never asked about.
    /// </remarks>
    public async Task ForgetAsync(
        Guid organizationId,
        string key,
        CancellationToken cancellationToken)
    {
        Require(organizationId, key);

        try
        {
            await client.DeleteObjectAsync(
                new DeleteObjectRequest { BucketName = _options.Bucket, Key = key },
                cancellationToken);
        }
        catch (AmazonS3Exception failure)
        {
            logger.LogWarning(
                failure,
                "Could not remove the stored object {Key}; it is now unreferenced.",
                key);
        }
    }

    /// <summary>
    /// Refuses a key that is not this organization's.
    /// </summary>
    /// <exception cref="InvalidOperationException">The key belongs elsewhere.</exception>
    private static void Require(Guid organizationId, string key)
    {
        if (!StorageKeys.Belongs(organizationId, key))
        {
            throw new InvalidOperationException(
                "The stored object does not belong to the active organization.");
        }
    }
}

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Options;

namespace SportFrog.Api.Infrastructure.Storage;

/// <summary>
/// Makes sure the bucket exists before anything tries to write to it.
/// </summary>
/// <remarks>
/// A first run against a fresh MinIO would otherwise fail on the first
/// photograph somebody uploaded, with an error about a bucket rather than
/// about anything they did — and whoever set the environment up would hear
/// about it from a user.
///
/// A failure here is logged and does not stop the process. Object storage
/// being unreachable is an outage of one part of the system, and the rest of
/// it — recording a result on a Sunday afternoon, reading a table — has
/// nothing to do with files. Refusing to start would turn a lost photograph
/// into a lost league.
/// </remarks>
internal sealed class StorageBootstrap(
    IAmazonS3 client,
    IOptions<StorageOptions> options,
    ILogger<StorageBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bucket = options.Value.Bucket;

        try
        {
            if (await AmazonS3Util.DoesS3BucketExistV2Async(client, bucket))
            {
                return;
            }

            await client.PutBucketAsync(
                new PutBucketRequest { BucketName = bucket }, cancellationToken);

            logger.LogInformation("Created the object storage bucket {Bucket}.", bucket);
        }
        catch (Exception failure) when (failure is AmazonS3Exception or HttpRequestException)
        {
            logger.LogError(
                failure,
                "Could not reach object storage to verify the bucket {Bucket}. "
                    + "Anything that stores or reads a file will fail until this is resolved.",
                bucket);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;

namespace SportFrog.Api.Infrastructure.Storage;

/// <summary>Registers object storage.</summary>
public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddSportFrogStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Validated on start, like the signing key: a missing access key is a
        // deployment that is wrong now, not one that will be found wrong the
        // first time a club uploads a squad.
        services
            .AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

            return new AmazonS3Client(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = options.Endpoint,

                    // MinIO addresses a bucket as a path segment. A virtual
                    // hosted deployment would need a wildcard certificate and
                    // a DNS entry per bucket, which is not what this is.
                    ForcePathStyle = options.ForcePathStyle,

                    // Nothing regional about a local container, but the
                    // signature is computed over a region, so both ends have
                    // to name the same one.
                    AuthenticationRegion = options.Region,
                });
        });

        services.AddSingleton<ObjectStore>();
        services.AddHostedService<StorageBootstrap>();

        return services;
    }
}

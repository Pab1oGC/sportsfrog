using Microsoft.Extensions.Caching.Memory;

namespace SportFrog.Api.Infrastructure.Caching;

/// <summary>Registers the public portal's query cache.</summary>
public static class CachingServiceCollectionExtensions
{
    public static IServiceCollection AddSportFrogPublicCaching(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IPublicQueryCache, PublicQueryCache>();

        return services;
    }
}

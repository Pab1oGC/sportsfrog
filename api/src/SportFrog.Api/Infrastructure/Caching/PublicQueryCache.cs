using Microsoft.Extensions.Caching.Memory;

namespace SportFrog.Api.Infrastructure.Caching;

/// <summary>
/// An <see cref="IMemoryCache"/>-backed <see cref="IPublicQueryCache"/>.
/// </summary>
/// <remarks>
/// In-process memory, not a distributed cache: the API runs as one instance
/// today (see <c>docker-compose.yml</c> — one <c>api</c> service), so a
/// second cache to keep consistent across nodes would buy nothing yet. The
/// day it does, only this class changes — every caller already depends on
/// <see cref="IPublicQueryCache"/>, not on this.
///
/// The absolute expiration is a safety net, not the freshness mechanism: a
/// write that changes cached data calls <see cref="Invalidate"/> and a
/// visitor sees it on their very next request. The expiration exists for
/// whatever that explicit call misses — a row changed by a future code path
/// that forgets to invalidate, a manual fix run straight against the
/// database — so staleness always has a ceiling even when the fast path
/// fails.
/// </remarks>
internal sealed class PublicQueryCache(IMemoryCache cache) : IPublicQueryCache
{
    private static readonly TimeSpan MaximumStaleness = TimeSpan.FromSeconds(30);

    public async Task<T> GetOrCreateAsync<T>(
        PublicCacheKey key, Func<CancellationToken, Task<T>> query, CancellationToken cancellationToken)
        where T : notnull
    {
        var cacheKey = key.ToCacheKey();

        if (cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await query(cancellationToken);

        cache.Set(cacheKey, value, MaximumStaleness);

        return value;
    }

    public void Invalidate(PublicCacheKey key) => cache.Remove(key.ToCacheKey());
}

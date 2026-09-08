using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using SportFrog.Api.Infrastructure.Caching;

namespace SportFrog.Api.Tests.Infrastructure.Caching;

/// <summary>
/// The read-through cache behind the public portal's more expensive
/// readings — see <see cref="ReadPublicClassification"/> for the actual
/// caller. What matters here is the contract every caller relies on:
/// a miss runs the query exactly once, a hit never runs it again, different
/// keys never share an entry, and an invalidated key goes back to missing.
/// </summary>
public sealed class PublicQueryCacheTests : IDisposable
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly PublicQueryCache _cache;

    public PublicQueryCacheTests()
    {
        _cache = new PublicQueryCache(_memoryCache);
    }

    public void Dispose() => _memoryCache.Dispose();

    [Fact]
    public async Task GetOrCreateAsync_FirstCall_RunsTheQuery()
    {
        var calls = 0;

        var result = await _cache.GetOrCreateAsync(
            PublicCacheKey.Classification(Guid.NewGuid()),
            _ => { calls++; return Task.FromResult("built"); },
            CancellationToken.None);

        result.Should().Be("built");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_SecondCallSameKey_ReturnsCachedValueWithoutRunningTheQueryAgain()
    {
        var key = PublicCacheKey.Classification(Guid.NewGuid());
        var calls = 0;

        Task<string> Query(CancellationToken _) { calls++; return Task.FromResult($"built-{calls}"); }

        var first = await _cache.GetOrCreateAsync(key, Query, CancellationToken.None);
        var second = await _cache.GetOrCreateAsync(key, Query, CancellationToken.None);

        second.Should().Be(first);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_DifferentCompetitions_NeverShareAnEntry()
    {
        var calls = 0;

        Task<string> Query(CancellationToken _) { calls++; return Task.FromResult($"built-{calls}"); }

        var first = await _cache.GetOrCreateAsync(
            PublicCacheKey.Classification(Guid.NewGuid()), Query, CancellationToken.None);
        var second = await _cache.GetOrCreateAsync(
            PublicCacheKey.Classification(Guid.NewGuid()), Query, CancellationToken.None);

        second.Should().NotBe(first);
        calls.Should().Be(2);
    }

    [Fact]
    public async Task Invalidate_ThenGetOrCreateAsync_RunsTheQueryAgain()
    {
        var key = PublicCacheKey.Classification(Guid.NewGuid());
        var calls = 0;

        Task<string> Query(CancellationToken _) { calls++; return Task.FromResult($"built-{calls}"); }

        var first = await _cache.GetOrCreateAsync(key, Query, CancellationToken.None);
        _cache.Invalidate(key);
        var second = await _cache.GetOrCreateAsync(key, Query, CancellationToken.None);

        second.Should().NotBe(first);
        calls.Should().Be(2);
    }

    [Fact]
    public void Invalidate_AKeyNeverCached_DoesNotThrow()
    {
        var act = () => _cache.Invalidate(PublicCacheKey.Classification(Guid.NewGuid()));

        act.Should().NotThrow();
    }
}

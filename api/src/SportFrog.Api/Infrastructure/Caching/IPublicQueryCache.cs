namespace SportFrog.Api.Infrastructure.Caching;

/// <summary>
/// A read-through cache for public-portal queries.
/// </summary>
/// <remarks>
/// An endpoint depends on this interface, never on a concrete cache — the
/// same reason <c>IMatchOutcomeRules</c> is an interface and not a call
/// straight into one implementation: what backs it (in-process memory today,
/// a distributed cache if this API is ever scaled out to more than one
/// instance) is a deployment detail the endpoint has no business knowing.
/// </remarks>
public interface IPublicQueryCache
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or runs
    /// <paramref name="query"/> and caches what it returns.
    /// </summary>
    /// <remarks>
    /// Concurrent misses on the same key may each run <paramref name="query"/>
    /// once rather than sharing a single in-flight read — a per-key lock
    /// would close that gap, at the cost of a lock table to leak-check for
    /// every key ever requested. For a read this cheap, on a portal this
    /// size, the duplicated work loses nothing a visitor can feel; the day it
    /// does, that guarantee belongs here, behind the same interface, without
    /// touching a single caller.
    /// </remarks>
    Task<T> GetOrCreateAsync<T>(
        PublicCacheKey key, Func<CancellationToken, Task<T>> query, CancellationToken cancellationToken)
        where T : notnull;

    /// <summary>
    /// Discards whatever is cached for <paramref name="key"/>, so the next
    /// read runs the query fresh.
    /// </summary>
    /// <remarks>
    /// Removing a key nobody ever cached is not an error — the classification
    /// stage this invalidates may never have been read publicly at all, and
    /// the write that changes it does not know or care either way.
    /// </remarks>
    void Invalidate(PublicCacheKey key);
}

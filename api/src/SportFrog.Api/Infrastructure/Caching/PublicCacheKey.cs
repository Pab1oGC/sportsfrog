namespace SportFrog.Api.Infrastructure.Caching;

/// <summary>
/// Identifies one cached public-portal response.
/// </summary>
/// <remarks>
/// Keyed by the domain entity a response describes, not by the address a
/// visitor typed. The public portal names things by slug
/// (<c>/public/{organizationSlug}/{competitionSlug}/...</c>), but a slug can
/// be renamed and two different addresses must never accidentally collide
/// on one cache entry — a stable internal id has neither problem. It also
/// means the code that invalidates a cache entry on a write (which knows a
/// <c>competitionId</c>, never a slug) can build the exact same key the
/// read built, without a lookup of its own.
///
/// One factory method per cacheable kind of response, all in this file, so
/// "what public data is cached and how it's keyed" has a single place to
/// read rather than a string literal repeated at every call site.
/// </remarks>
public readonly record struct PublicCacheKey
{
    private readonly string _section;
    private readonly Guid _id;

    private PublicCacheKey(string section, Guid id)
    {
        _section = section;
        _id = id;
    }

    /// <summary>
    /// A judged category's classification stage, for every category of one
    /// competition at once — the same unit <c>ReadPublicClassification</c>
    /// reads and <c>OpenClassificationStage</c>/<c>RecordPerformance</c>
    /// invalidate.
    /// </summary>
    public static PublicCacheKey Classification(Guid competitionId) => new("classification", competitionId);

    /// <summary>
    /// The literal cache key. Exposed only for the cache implementation
    /// itself — callers compare and pass <see cref="PublicCacheKey"/> values,
    /// never this string.
    /// </summary>
    internal string ToCacheKey() => $"public:{_section}:{_id:N}";
}

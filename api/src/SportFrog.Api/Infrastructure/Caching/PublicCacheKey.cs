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
    /// A competition's public page — everything <c>ReadPublicCompetition</c>
    /// answers except its <c>PortalMoment</c>, which is a fact read off
    /// "right now" and is never cached under any key.
    /// </summary>
    /// <remarks>
    /// Invalidated by every writer that changes something the shell shows:
    /// <c>UpdateCompetition</c> and <c>ChangeCompetitionStatus</c> for the
    /// competition's own fields (its settings, its status), and
    /// <c>CreateCategory</c>/<c>UpdateCategory</c>/<c>DeleteCategory</c> and
    /// <c>CreateTeam</c>/<c>EnrollIndividual</c>/<c>DeleteTeam</c> for its
    /// divisions and their team counts. Not by <c>PublishCompetition</c>:
    /// whether a competition is public decides if the resolver ever reaches
    /// this key at all, not what the shell holds once it does. Not by
    /// <c>UpdateTeam</c> either: a team's name, seed and active flag are not
    /// part of the shell, only how many a category has, which renaming one
    /// does not change. Whatever a future writer still misses is bounded by
    /// <see cref="IPublicQueryCache"/>'s own expiration ceiling in the
    /// meantime.
    /// </remarks>
    public static PublicCacheKey CompetitionPage(Guid competitionId) => new("competition-page", competitionId);

    /// <summary>
    /// The literal cache key. Exposed only for the cache implementation
    /// itself — callers compare and pass <see cref="PublicCacheKey"/> values,
    /// never this string.
    /// </summary>
    internal string ToCacheKey() => $"public:{_section}:{_id:N}";
}

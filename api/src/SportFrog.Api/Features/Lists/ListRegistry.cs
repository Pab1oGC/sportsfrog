using System.Diagnostics.CodeAnalysis;

namespace SportFrog.Api.Features.Lists;

/// <summary>Resolves a list by the slug it is cataloged and requested under.</summary>
internal interface IListRegistry
{
    /// <summary>Every list known to the catalog, in the order they were registered.</summary>
    IReadOnlyList<IListProvider> All { get; }

    /// <summary>
    /// The provider for <paramref name="slug"/>. Throws when none is
    /// registered — for a caller that named the slug itself and treats an
    /// unregistered one as a wiring mistake, not an answer to handle.
    /// </summary>
    IListProvider For(string slug);

    /// <summary>
    /// The provider for <paramref name="slug"/>, or false when none is
    /// registered — for a caller handing back a slug an HTTP request typed
    /// in, where "no such list" is an ordinary outcome and not one worth
    /// raising an exception to report.
    /// </summary>
    bool TryFor(string slug, [NotNullWhen(true)] out IListProvider? provider);
}

/// <summary>
/// Built from every <see cref="IListProvider"/> the container knows about,
/// keyed by the slug each one declares for itself.
/// </summary>
/// <remarks>
/// Constructed from <see cref="IEnumerable{T}"/> so the container's own
/// registrations are the only list of lists this ever reads — the same
/// choice <see cref="SportFrog.Api.Features.Draw.CalendarDrawRegistry"/>
/// makes for calendar formats.
/// </remarks>
internal sealed class ListRegistry : IListRegistry
{
    private readonly IReadOnlyDictionary<string, IListProvider> providersBySlug;

    public ListRegistry(IEnumerable<IListProvider> providers)
    {
        var all = providers.ToList();

        // A duplicate slug registered twice is a wiring mistake and should
        // fail at startup rather than have the catalog silently expose
        // whichever happened to be registered last.
        providersBySlug = all.ToDictionary(provider => provider.Slug, StringComparer.Ordinal);

        All = all;
    }

    public IReadOnlyList<IListProvider> All { get; }

    public IListProvider For(string slug) =>
        providersBySlug.TryGetValue(slug, out var provider)
            ? provider
            : throw new InvalidOperationException($"No hay una lista registrada con el slug '{slug}'.");

    public bool TryFor(string slug, [NotNullWhen(true)] out IListProvider? provider) =>
        providersBySlug.TryGetValue(slug, out provider);
}

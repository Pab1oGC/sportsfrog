using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Storage;

/// <summary>
/// A competition's own mark and accent colour, read once for whichever
/// document is about to draw them.
/// </summary>
/// <remarks>
/// Four documents want exactly this pair — the fixture, the bulletin, a
/// team's report, an athlete's — and before this, three of them read it by
/// hand and a fourth had its own near-identical copy of the same query.
/// One reader instead, for the reason every other shared query in this
/// codebase exists: two readings of the same fact are two facts, and the
/// day a competition's mark and a competition's colour come from different
/// places is the day a document shows one competition's logo next to
/// another's colour.
/// </remarks>
/// <param name="LogoBytes">The competition's own mark, already read from storage. Null when it never set one.</param>
/// <param name="AccentColor">
/// The resolved theme's primary colour, as "#rrggbb" — folding in the older
/// single accent colour the same way the public page does. Null when the
/// competition never dressed up its page at all, which every caller reads
/// as "draw this the way every document always has".
/// </param>
public sealed record CompetitionBranding(byte[]? LogoBytes, string? AccentColor)
{
    /// <summary>Neither a mark nor a colour — a competition that could not be found at all.</summary>
    public static readonly CompetitionBranding None = new(null, null);

    /// <summary>
    /// Reads a competition's branding by id, loading the row itself.
    /// </summary>
    /// <remarks>
    /// For a caller that only has the id — a team's or an athlete's report,
    /// composed from the team or athlete outward rather than from the
    /// competition in. <see cref="FromAsync"/> is the cheaper alternative
    /// once a caller already has the row, which the fixture and the
    /// bulletin both do.
    /// </remarks>
    public static async Task<CompetitionBranding> ReadAsync(
        SportFrogDbContext database, ObjectStore store, Guid competitionId, CancellationToken cancellationToken)
    {
        // Loaded whole, not projected: Settings is a jsonb column read back
        // as a real CompetitionSettings object once the row is materialized
        // — EF cannot translate a path into it inside a Select.
        var competition = await database.Competitions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == competitionId, cancellationToken);

        return competition is null ? None : await FromAsync(store, competition, cancellationToken);
    }

    /// <summary>Reads a competition's branding from a row the caller already has.</summary>
    public static async Task<CompetitionBranding> FromAsync(
        ObjectStore store, Competition competition, CancellationToken cancellationToken)
    {
        var settings = competition.Settings.Public;

        var logoBytes = settings?.LogoKey is { } logoKey
            ? await store.ReadAsync(competition.OrgId, logoKey, cancellationToken)
            : null;

        return new CompetitionBranding(logoBytes, PortalTheme.Resolve(settings)?.Primary);
    }
}

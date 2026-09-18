using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// When and where a published competition is played, and how it ended.
/// </summary>
/// <remarks>
/// Not behind a setting, unlike the table and the boards. Publishing a
/// competition is mostly an act of publishing this: a parent looking up what
/// time the match is on Sunday is the reason the public page exists, and a
/// competition that wanted to keep its fixtures private would not be
/// published at all.
///
/// It is deliberately not the same projection the organization reads. That
/// one carries the notes a referee or an organizer wrote on the fixture,
/// which are working notes about people and belong inside the organization.
/// Sharing the projection to save a few lines would have published them by
/// accident the first time somebody added a field.
/// </remarks>
public static class ReadPublicCalendar
{
    public sealed record Fixture(
        Guid Id,
        Guid CategoryId,
        string CategoryName,

        /// <summary>
        /// Null for a knockout slot drawn in full whose side is still
        /// "whoever wins another match" — see <see cref="HomePlaceholder"/>
        /// for what to show instead.
        /// </summary>
        string? HomeTeamName,

        /// <summary>
        /// "Ganador de {phase}", set exactly when <see cref="HomeTeamName"/>
        /// is not — the fixture whose winner still has to fill this side.
        /// </summary>
        string? HomePlaceholder,
        string? HomeClubLogoUrl,

        /// <summary>The club a home entry plays under. Never withheld — the same fact the crest already carries.</summary>
        string? HomeClubName,
        string? AwayTeamName,

        /// <summary>See <see cref="HomePlaceholder"/>; the same story, the other side.</summary>
        string? AwayPlaceholder,
        string? AwayClubLogoUrl,

        /// <summary>The club an away entry plays under. Never withheld — the same fact the crest already carries.</summary>
        string? AwayClubName,

        /// <summary>
        /// A home competitor's own photo, signed — present only for an
        /// individual-sport entry of exactly one athlete, on a competition
        /// that opted into "show athlete photos" (<c>PublicSettings.
        /// ShowAthletePhotos</c>). Null for everything else: a team sport, a
        /// poomsae pair or trio
        /// (whose "photo" is not one well-defined thing), or an organization
        /// that never turned the switch on. See <c>HandleAsync</c> for the
        /// full reasoning — this is the one field on this record that is not
        /// simply "whatever the match says."
        /// </summary>
        string? HomePhotoUrl,

        /// <summary>The away side's own photo, same conditions as <see cref="HomePhotoUrl"/>.</summary>
        string? AwayPhotoUrl,
        string? VenueName,
        string? VenueMapsUrl,
        string? SpaceName,
        DateTimeOffset? ScheduledAt,
        short? RoundNumber,
        string? Phase,
        MatchState Status,
        int? HomeTotal,
        int? AwayTotal,

        /// <summary>The shootout that broke a level knockout match, once it needed one.</summary>
        short? PenaltyHomeScore,
        short? PenaltyAwayScore,

        /// <summary>
        /// The score read from events recorded so far, while the match is
        /// still being played. Set only while <see cref="Status"/> is
        /// <see cref="MatchState.InProgress"/>.
        /// </summary>
        int? LiveHomeTotal,
        int? LiveAwayTotal);

    public sealed record Response(IReadOnlyList<Fixture> Fixtures);

    public static IEndpointRouteBuilder MapReadPublicCalendar(this IEndpointRouteBuilder routes)
    {
        routes.MapGet($"{PublicRoutes.Prefix}/matches", HandleAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicCalendar))
            .WithSummary("Reads the calendar of a published competition.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        string organizationSlug,
        string competitionSlug,
        PublicCompetitionReader reader,
        ObjectStore store,
        CancellationToken cancellationToken,
        Guid? categoryId = null,
        string? status = null,
        short? round = null)
    {
        // A query parameter reaches no validator. An unreadable state is
        // ignored rather than refused: a visitor followed a link, and the
        // useful answer to a mistyped filter is the calendar rather than a
        // problem document.
        var state = WireEnum.TryParse<MatchState>(QueryFilter.OrAbsent(status), out var parsed)
            ? parsed
            : (MatchState?)null;

        var page = await reader.ReadAsync(
            organizationSlug,
            competitionSlug,
            async (database, resolved) =>
            {
                // Read as the storage keys first — EF translates this Select
                // to SQL, and signing a link is not something a query can do.
                var matches = await database.Matches
                    .AsNoTracking()
                    .Where(match => match.CompetitionId == resolved.CompetitionId)
                    .Where(match => categoryId == null || match.CategoryId == categoryId)
                    .Where(match => state == null || match.Status == state)
                    .Where(match => round == null || match.RoundNumber == round)

                    // Chronological with the undated last, which is where a
                    // fixture drawn but not yet placed belongs on a page
                    // somebody is reading to find out when they play. Phase
                    // comes before round number in the tie-break: a category
                    // promoted to a knockout restarts its round count at one,
                    // same as its group stage did, so round alone cannot
                    // tell a group's first jornada from the bracket's first
                    // round — only Phase, null for one and set for the
                    // other, can.
                    .OrderBy(match => match.ScheduledAt == null)
                    .ThenBy(match => match.ScheduledAt)
                    .ThenBy(match => match.Phase != null)
                    .ThenBy(match => match.RoundNumber)
                    .Select(match => new
                    {
                        match.Id,
                        match.CategoryId,
                        CategoryName = match.Category!.Name,
                        match.HomeTeamId,
                        HomeTeamName = match.HomeTeam!.Name,
                        HomePlaceholder = match.HomeTeamId == null && match.HomeSourceMatch != null
                            ? "Ganador de " + match.HomeSourceMatch.Phase
                            : null,
                        HomeLogoKey = match.HomeTeam.Club!.LogoUrl,
                        HomeClubName = match.HomeTeam.Club.Name,
                        match.AwayTeamId,
                        AwayTeamName = match.AwayTeam!.Name,
                        AwayPlaceholder = match.AwayTeamId == null && match.AwaySourceMatch != null
                            ? "Ganador de " + match.AwaySourceMatch.Phase
                            : null,
                        AwayLogoKey = match.AwayTeam.Club!.LogoUrl,
                        AwayClubName = match.AwayTeam.Club.Name,
                        VenueName = match.VenueSpace!.Venue!.Name,
                        VenueMapsUrl = match.VenueSpace.Venue.MapsUrl,
                        SpaceName = match.VenueSpace.Name,
                        match.ScheduledAt,
                        match.RoundNumber,
                        match.Phase,
                        match.Status,
                        match.HomeTotal,
                        match.AwayTotal,
                        match.PenaltyHomeScore,
                        match.PenaltyAwayScore,
                    })
                    .ToListAsync(cancellationToken);

                // In progress implies both teams are already named.
                var liveTotals = await LiveTotalsAsync(database, matches
                    .Where(match => match.Status == MatchState.InProgress)
                    .Select(match => (match.Id, match.HomeTeamId!.Value, match.AwayTeamId!.Value))
                    .ToList(),
                    cancellationToken);

                var teamIds = matches
                    .SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId })
                    .Where(id => id is not null)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();

                var photoKeys = await AthletePhotoKeysAsync(
                    database, resolved.CompetitionId, teamIds, cancellationToken);

                var fixtures = new List<Fixture>(matches.Count);

                foreach (var match in matches)
                {
                    // TryGetValue, not GetValueOrDefault: Totals is a value
                    // type, so a missing key's default(Totals) is (0, 0) —
                    // GetValueOrDefault would hand that back as a Totals?
                    // that HasValue, indistinguishable from a real 0-0.
                    // liveTotals only carries an entry for a match whose
                    // sport derives a live score at all (see LiveTotalsAsync),
                    // so a genuinely missing key has to stay null here.
                    LiveScore.Totals? live = liveTotals.TryGetValue(match.Id, out var totals)
                        ? totals
                        : null;

                    fixtures.Add(new Fixture(
                        match.Id,
                        match.CategoryId,
                        match.CategoryName,
                        match.HomeTeamName,
                        match.HomePlaceholder,
                        await LinkAsync(store, resolved.OrganizationId, match.HomeLogoKey, cancellationToken),
                        match.HomeClubName,
                        match.AwayTeamName,
                        match.AwayPlaceholder,
                        await LinkAsync(store, resolved.OrganizationId, match.AwayLogoKey, cancellationToken),
                        match.AwayClubName,
                        await LinkAsync(store, resolved.OrganizationId, match.HomeTeamId is { } homeId ? photoKeys.GetValueOrDefault(homeId) : null, cancellationToken),
                        await LinkAsync(store, resolved.OrganizationId, match.AwayTeamId is { } awayId ? photoKeys.GetValueOrDefault(awayId) : null, cancellationToken),
                        match.VenueName,
                        match.VenueMapsUrl,
                        match.SpaceName,
                        match.ScheduledAt,
                        match.RoundNumber,
                        match.Phase,
                        match.Status,
                        match.HomeTotal,
                        match.AwayTotal,
                        match.PenaltyHomeScore,
                        match.PenaltyAwayScore,
                        live?.Home,
                        live?.Away));
                }

                return new Response(fixtures);
            },
            cancellationToken);

        return page is null ? Results.NotFound() : Results.Ok(page);
    }

    /// <summary>
    /// The live score of every match still in progress, keyed by match.
    /// </summary>
    /// <remarks>
    /// See <see cref="SportFrog.Api.Features.Matches.ReadMatches"/>'s copy of this same shape — the
    /// tally lives in <see cref="LiveScore"/> and is tested there; this is
    /// just the query that feeds it, repeated because the public projection
    /// reads through <see cref="PublicCompetitionReader"/> rather than the
    /// organization's own <c>SportFrogDbContext</c> access pattern, so the two
    /// cannot easily share one query method without sharing far more than
    /// this one.
    /// </remarks>
    private static async Task<Dictionary<Guid, LiveScore.Totals>> LiveTotalsAsync(
        SportFrogDbContext database,
        List<(Guid MatchId, Guid HomeTeamId, Guid AwayTeamId)> liveMatches,
        CancellationToken cancellationToken)
    {
        if (liveMatches.Count == 0)
        {
            return [];
        }

        var candidateIds = liveMatches.Select(match => match.MatchId).ToList();

        // A live score only exists for a sport that derives one from events
        // at all — see LiveScore.AppliesTo, which states the same rule this
        // mirrors. Not called directly: EF Core cannot translate a call into
        // it, and asking for the rest would not crash anyway — Compute would
        // return zero for every one of them, which is not a live score, it
        // is what "nothing to tally" looks like, and showing it as EN VIVO
        // 0-0 for the length of a set-scored match says something that never
        // happened.
        var liveIds = await database.Matches
            .AsNoTracking()
            .Where(match => candidateIds.Contains(match.Id))
            .Where(match => match.Competition!.Sport!.ScoreMode == ScoreMode.Cumulative)
            .Select(match => match.Id)
            .ToListAsync(cancellationToken);

        if (liveIds.Count == 0)
        {
            return [];
        }

        var events = await database.PlayerEvents
            .AsNoTracking()
            .Where(recorded => liveIds.Contains(recorded.MatchId) && recorded.Metric!.AffectsScore)
            .Select(recorded => new
            {
                recorded.MatchId,
                TeamId = recorded.RosterEntry!.TeamId,
                recorded.Metric!.ScorePoints,
                recorded.Metric.CountsForOpponent,
                recorded.Quantity,
            })
            .ToListAsync(cancellationToken);

        var byMatch = events.ToLookup(recorded => recorded.MatchId);
        var liveIdSet = liveIds.ToHashSet();

        return liveMatches
            .Where(match => liveIdSet.Contains(match.MatchId))
            .ToDictionary(
                match => match.MatchId,
                match => LiveScore.Compute(
                    byMatch[match.MatchId].Select(recorded => new ScoringEvent(
                        recorded.TeamId, recorded.ScorePoints, recorded.CountsForOpponent, recorded.Quantity)),
                    match.HomeTeamId,
                    match.AwayTeamId));
    }

    /// <summary>
    /// The storage key of the one athlete's photo behind each team that
    /// qualifies for one, keyed by team — never touched for a team sport or
    /// an organization that left the switch off, and never resolved for a
    /// team fielding more than one athlete (a poomsae pair or trio), since
    /// "the competitor's photo" is not one well-defined thing there.
    /// </summary>
    internal static async Task<Dictionary<Guid, string?>> AthletePhotoKeysAsync(
        SportFrogDbContext database,
        Guid competitionId,
        List<Guid> teamIds,
        CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
        {
            return [];
        }

        // `Settings` comes back whole, never a property reached inside it --
        // it is mapped with a value converter over the entire jsonb column
        // (see CompetitionConfiguration), so EF can only translate a
        // projection that asks for the whole object. Reaching for
        // `Settings.Public.ShowAthletePhotos` directly inside this Select
        // silently produced false regardless of the real value; the fix is
        // the same shape ReadPublicCompetition already uses everywhere else
        // it reads a Settings.Public field: fetch Settings whole, then
        // navigate it in plain C# below.
        var competition = await database.Competitions
            .AsNoTracking()
            .Where(candidate => candidate.Id == competitionId)
            .Select(candidate => new { IsIndividual = candidate.Sport!.IsIndividual, candidate.Settings })
            .SingleAsync(cancellationToken);

        // Absent settings mean the defaults PublicSettings declares -- for
        // this switch, off, same reasoning ReadPublicCompetition already
        // applies to every other field of Settings.Public.
        var showAthletePhotos = competition.Settings.Public?.ShowAthletePhotos ?? false;

        if (!competition.IsIndividual || !showAthletePhotos)
        {
            return [];
        }

        var entries = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => teamIds.Contains(entry.TeamId) && entry.WithdrawnAt == null)
            .Select(entry => new { entry.TeamId, entry.Athlete!.PhotoKey })
            .ToListAsync(cancellationToken);

        return entries
            .GroupBy(entry => entry.TeamId)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().PhotoKey);
    }

    /// <summary>A club's crest, signed — or null, for a club that never uploaded one.</summary>
    private static Task<string?> LinkAsync(
        ObjectStore store, Guid organizationId, string? key, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(key)
            ? Task.FromResult<string?>(null)
            : store.ReadLinkAsync(organizationId, key, cancellationToken);
}

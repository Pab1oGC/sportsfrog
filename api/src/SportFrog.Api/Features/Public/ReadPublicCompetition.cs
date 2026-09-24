using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Standings;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// The page a competition resolves to, for anybody with the address.
/// </summary>
/// <remarks>
/// The first thing that ever calls <c>resolve_public_competition</c>. The
/// address is two readable segments — the organization's and the
/// competition's — and never an identifier: a public URL that carried one
/// would invite walking the others.
///
/// Everything the reader hands back has already passed three checks made by
/// the database: the organization exists and is active, the competition
/// belongs to it, and it is published. An address failing any of them is
/// answered as not found, and the three are indistinguishable from outside
/// on purpose (RNF-16) — knowing a slug is not authorization to learn whether
/// an organization is suspended.
/// </remarks>
public static class ReadPublicCompetition
{
    /// <param name="Shows">
    /// Which sections this competition publishes. Returned so a page knows
    /// which tabs to draw rather than discovering it by asking for each one
    /// and being refused.
    /// </param>
    public sealed record Response(
        string OrganizationName,

        /// <summary>The organization's own mark, wherever it runs competitions.</summary>
        string? OrganizationLogoUrl,
        string Name,
        string Season,
        string SportCode,
        string SportName,

        /// <summary>What one unit of score is called: "gol", "punto".</summary>
        string ScoringUnit,

        /// <summary>What one division of a match is called: "tiempo", "set".</summary>
        string PeriodLabel,

        /// <summary>
        /// Whether this sport is decided by periods won rather than a total
        /// score, so a standings column or a tiebreaker label never has to
        /// guess it from the sport's name. See
        /// <see cref="Rulebook.ReadSports.Summary"/>, which exposes the same
        /// fact to the organization's own pages.
        /// </summary>
        bool IsPlayedInSets,

        /// <summary>
        /// Whether this sport is decided by a score judges hand down rather
        /// than a table — poomsae, not yet any other sport in the catalog.
        /// The other branch a public page needs alongside
        /// <see cref="IsPlayedInSets"/>: a judged category has a
        /// classification to rank, not a standings table to show.
        /// </summary>
        bool IsJudged,

        /// <summary>
        /// Whether the unit that plays is a person rather than a club —
        /// taekwondo, not yet any other sport in the catalog. The public
        /// page reads this to know whether its "por equipo" filter should
        /// say "Equipo" or "Deportista": a team row in an individual sport
        /// already carries one athlete's own name (see
        /// <c>Teams.IndividualTeamName</c>), so calling it a team is the
        /// one thing on this page that would read as wrong to a spectator.
        /// </summary>
        bool IsIndividual,

        string Format,
        CompetitionState Status,
        DateOnly? StartsOn,
        DateOnly? EndsOn,
        Sections Shows,
        IReadOnlyList<CategorySummary> Categories,
        Portal Portal,
        PortalMoment Moment);

    /// <param name="Bracket">
    /// Whether this competition has at least one match with a knockout
    /// phase, anywhere across its categories. No organizer switch controls
    /// this one, the same way none controls the calendar — an empty tab
    /// would just be the "Sin datos de posiciones" bug this mirrors,
    /// worn by a different section, so the tab does not exist at all until
    /// a bracket genuinely does. The category a visitor happens to have
    /// selected can still be the one without a phase; that reads as an
    /// empty bracket, not a missing tab, the same way a judged category
    /// with nothing scored yet still has a Classification tab.
    /// </param>
    public sealed record Sections(bool Standings, bool Leaders, bool Rosters, bool Classification, bool Gallery, bool Bracket);

    public sealed record CategorySummary(
        Guid Id,
        string Name,
        string? Gender,
        int TeamCount);

    /// <summary>
    /// However this competition dressed up its own page, on top of the plain
    /// one every competition gets. Every field is absent unless somebody set
    /// it, which is what keeps a page that never opened these settings
    /// looking exactly as it always did.
    /// </summary>
    /// <param name="AccentColor">
    /// The pre-theme single accent colour, still sent for a client that only
    /// reads this. When <paramref name="Theme"/> is present it equals its
    /// primary; when neither is set it is null.
    /// </param>
    /// <param name="Theme">
    /// The full visual system, already resolved: defaults filled, the legacy
    /// accent colour folded in. Null means the competition never dressed its
    /// page up and the client should render on the plain SportFrog theme —
    /// the same signal the whole record being sparse has always given.
    /// </param>
    /// <param name="SectionOrder">
    /// The page's own sections — standings, leaders, classification, the
    /// calendar, the gallery — in the order they render, each already
    /// carrying the name it renders under. Always all five: see
    /// <see cref="PortalSection.Resolve"/>. Whether one of them is skipped
    /// over entirely is still <see cref="Sections"/> above and
    /// <see cref="Gallery"/> being non-empty — this only ever answers order
    /// and naming, never whether a section is drawn at all.
    /// </param>
    /// <param name="Gallery">Photos from the event itself, in the order they were set to appear.</param>
    public sealed record Portal(
        string? BannerUrl,
        string? LogoUrl,
        string? AccentColor,
        ResolvedPortalTheme? Theme,
        IReadOnlyList<ResolvedPortalSection> SectionOrder,
        string? Description,
        string? Instagram,
        string? Facebook,
        string? WhatsApp,
        string? Website,
        IReadOnlyList<SponsorSummary> Sponsors,
        IReadOnlyList<GalleryPhotoSummary> Gallery);

    public sealed record SponsorSummary(string? Name, string? Url, string LogoUrl);

    public sealed record GalleryPhotoSummary(string? Caption, string Url);

    /// <summary>
    /// What the cover should say about the competition right now — never a
    /// choice an organizer made, only a fact read off its matches at the
    /// moment the page happens to be read. A visitor an hour later gets a
    /// different one, which is the point: an "EN VIVO" badge that never
    /// changes is not live.
    /// </summary>
    /// <param name="NextMatchAt">
    /// The earliest scheduled kickoff, while nothing has been played yet.
    /// Null once the competition is under way or finished, and equally null
    /// while nothing has been dated — a countdown to nothing is not a
    /// countdown.
    /// </param>
    /// <param name="LiveMatchCount">
    /// How many matches this competition has under way right now. Zero
    /// whenever the competition itself is not <see cref="CompetitionState.InProgress"/>
    /// — a stray match left open on a competition otherwise finished does
    /// not put the whole page back on air.
    /// </param>
    /// <param name="Champion">
    /// Who won, once that question has one honest answer. See
    /// <see cref="ResolveChampionAsync"/> for exactly when it does.
    /// </param>
    /// <param name="LiveMatch">
    /// The one match under way right now, for the Live hero variant — only
    /// answered when <paramref name="LiveMatchCount"/> is exactly 1. See
    /// <see cref="ResolveLiveMatchAsync"/> for exactly when it does; the same
    /// "don't guess which one" rule <see cref="ResolveChampionAsync"/> already
    /// follows for the champion.
    /// </param>
    public sealed record PortalMoment(
        DateTimeOffset? NextMatchAt, int LiveMatchCount, ChampionSummary? Champion, LiveMatchSummary? LiveMatch);

    public sealed record ChampionSummary(string TeamName, string? LogoUrl);

    /// <summary>
    /// The teams and score of the one match a competition has in progress
    /// right now. <see cref="HomeScore"/>/<see cref="AwayScore"/> stay null
    /// for a sport whose <see cref="ScoreMode"/> is not
    /// <see cref="ScoreMode.Cumulative"/> — sets and judged scores have no
    /// running tally to show mid-match, only the teams are worth naming.
    /// </summary>
    public sealed record LiveMatchSummary(
        string CategoryName,
        string HomeTeamName,
        string? HomeClubLogoUrl,
        string AwayTeamName,
        string? AwayClubLogoUrl,
        int? HomeScore,
        int? AwayScore);

    public static IEndpointRouteBuilder MapReadPublicCompetition(
        this IEndpointRouteBuilder routes)
    {
        routes.MapGet(PublicRoutes.Prefix, HandleAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicCompetition))
            .WithSummary("Reads a published competition by its public address.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        string organizationSlug,
        string competitionSlug,
        PublicCompetitionReader reader,
        ObjectStore store,
        CancellationToken cancellationToken)
    {
        var page = await reader.ReadAsync(
            organizationSlug,
            competitionSlug,
            async (database, resolved) =>
            {
                var competition = await database.Competitions
                    .AsNoTracking()
                    .Where(candidate => candidate.Id == resolved.CompetitionId)
                    .Select(candidate => new
                    {
                        candidate.Name,
                        candidate.Season,
                        candidate.SportCode,
                        SportName = candidate.Sport!.Name,
                        ScoringUnit = candidate.Sport.ScoringUnit,
                        PeriodLabel = candidate.Sport.PeriodLabel,
                        IsPlayedInSets = candidate.Sport.ScoreMode == ScoreMode.Sets,
                        IsJudged = candidate.Sport.ScoreMode == ScoreMode.Judged,
                        IsIndividual = candidate.Sport.IsIndividual,
                        candidate.Format,
                        candidate.Status,
                        candidate.StartsOn,
                        candidate.EndsOn,
                        candidate.Settings,
                        Organization = database.Organizations
                            .Where(org => org.Id == resolved.OrganizationId)
                            .Select(org => new { org.Name, org.LogoUrl })
                            .First(),
                    })

                    // Single rather than SingleOrDefault: the resolver found
                    // this row a moment ago, under this same context and this
                    // same transaction. Absent here would not be a missing
                    // page, it would be the isolation context having come
                    // undone between two statements — which is worth throwing
                    // over rather than answering with a tidy 404.
                    .SingleAsync(cancellationToken);

                var categories = await database.Categories
                    .AsNoTracking()
                    .Where(category => category.CompetitionId == resolved.CompetitionId)
                    .OrderBy(category => category.DisplayOrder)
                    .ThenBy(category => category.Name)
                    .Select(category => new CategorySummary(
                        category.Id,
                        category.Name,
                        category.Gender,
                        database.Teams.Count(team => team.CategoryId == category.Id)))
                    .ToListAsync(cancellationToken);

                var shows = competition.Settings.Public;

                var organizationLogoUrl = string.IsNullOrEmpty(competition.Organization.LogoUrl)
                    ? null
                    : await store.ReadLinkAsync(
                        resolved.OrganizationId, competition.Organization.LogoUrl, cancellationToken);

                var bannerUrl = string.IsNullOrEmpty(shows?.BannerKey)
                    ? null
                    : await store.ReadLinkAsync(resolved.OrganizationId, shows.BannerKey, cancellationToken);

                var competitionLogoUrl = string.IsNullOrEmpty(shows?.LogoKey)
                    ? null
                    : await store.ReadLinkAsync(resolved.OrganizationId, shows.LogoKey, cancellationToken);

                var sponsors = new List<SponsorSummary>();

                foreach (var sponsor in shows?.Sponsors ?? [])
                {
                    if (await store.ReadLinkAsync(resolved.OrganizationId, sponsor.LogoKey, cancellationToken)
                        is { } logoUrl)
                    {
                        sponsors.Add(new SponsorSummary(sponsor.Name, sponsor.Url, logoUrl));
                    }
                }

                var gallery = new List<GalleryPhotoSummary>();

                foreach (var photo in shows?.Gallery ?? [])
                {
                    if (await store.ReadLinkAsync(resolved.OrganizationId, photo.Key, cancellationToken)
                        is { } photoUrl)
                    {
                        gallery.Add(new GalleryPhotoSummary(photo.Caption, photoUrl));
                    }
                }

                // Defaults filled and the legacy accent colour folded in here,
                // once, rather than on the page: a stored theme is deliberately
                // sparse and every client would otherwise re-derive the same
                // fallbacks. Null when the competition never dressed its page
                // up, which the client reads as "stay on the plain theme".
                var theme = PortalTheme.Resolve(shows);

                var sectionOrder = PortalSection.Resolve(shows?.SectionOrder);

                var moment = await MomentAsync(
                    database, store, resolved, competition.Status, categories, cancellationToken);

                // Competition-wide, not per category: the tab either exists
                // or it does not, and which category a visitor happens to
                // have selected is a question for what's inside it, not for
                // whether it's there at all — see Sections.Bracket.
                var hasBracket = await database.Matches
                    .AsNoTracking()
                    .AnyAsync(
                        match => match.CompetitionId == resolved.CompetitionId && match.Phase != null,
                        cancellationToken);

                return new Response(
                    competition.Organization.Name,
                    organizationLogoUrl,
                    competition.Name,
                    competition.Season,
                    competition.SportCode,
                    competition.SportName,
                    competition.ScoringUnit,
                    competition.PeriodLabel,
                    competition.IsPlayedInSets,
                    competition.IsJudged,
                    competition.IsIndividual,
                    competition.Format,
                    competition.Status,
                    competition.StartsOn,
                    competition.EndsOn,

                    // Absent settings mean the defaults the record declares:
                    // standings, leaders, classification and the gallery
                    // shown, rosters not. A competition published without
                    // ever opening its settings still has a page worth
                    // reading, and its rosters still stay private.
                    //
                    // Standings is refused outright for a knockout-only
                    // competition, regardless of the organizer's own
                    // switch: single elimination has no group stage to
                    // tabulate, so the switch has nothing honest to turn on
                    // (see the same rule in StandingsQuery, which answers
                    // null for the same reason).
                    new Sections(
                        competition.Format != CompetitionFormat.Knockout && (shows?.ShowStandings ?? true),
                        shows?.ShowLeaders ?? true,
                        shows?.ShowRosters ?? false,
                        shows?.ShowClassification ?? true,
                        shows?.ShowGallery ?? true,
                        hasBracket),
                    categories,
                    new Portal(
                        bannerUrl,
                        competitionLogoUrl,
                        theme?.Primary ?? shows?.AccentColor,
                        theme,
                        sectionOrder,
                        shows?.Description,
                        shows?.Instagram,
                        shows?.Facebook,
                        shows?.WhatsApp,
                        shows?.Website,
                        sponsors,
                        gallery),
                    moment);
            },
            cancellationToken);

        // One answer for every way of not being readable: wrong organization,
        // wrong competition, suspended, unpublished. Distinguishing them here
        // would hand an address-guesser a map.
        return page is null ? Results.NotFound() : Results.Ok(page);
    }

    /// <summary>
    /// Reads the one fact about "right now" that matches this competition's
    /// own status: the next kickoff while it hasn't started, how many
    /// matches are live while it's under way, or who won once it's over.
    /// Nothing is read that the resulting page would not use — a finished
    /// competition's cover has no use for a live count, so none is queried.
    /// </summary>
    private static async Task<PortalMoment> MomentAsync(
        SportFrogDbContext database,
        ObjectStore store,
        PublicCompetition resolved,
        CompetitionState status,
        IReadOnlyList<CategorySummary> categories,
        CancellationToken cancellationToken)
    {
        if (status is CompetitionState.Draft or CompetitionState.Scheduled)
        {
            var nextMatchAt = await database.Matches
                .AsNoTracking()
                .Where(match => match.CompetitionId == resolved.CompetitionId)
                .Where(match => match.Status == MatchState.Scheduled && match.ScheduledAt != null)
                .OrderBy(match => match.ScheduledAt)
                .Select(match => match.ScheduledAt)
                .FirstOrDefaultAsync(cancellationToken);

            return new PortalMoment(nextMatchAt, 0, null, null);
        }

        if (status == CompetitionState.InProgress)
        {
            var liveMatchCount = await database.Matches
                .AsNoTracking()
                .CountAsync(
                    match => match.CompetitionId == resolved.CompetitionId
                        && match.Status == MatchState.InProgress,
                    cancellationToken);

            // Which one, not just how many — only worth asking when there is
            // exactly one to name. See ResolveLiveMatchAsync.
            var liveMatch = liveMatchCount == 1
                ? await ResolveLiveMatchAsync(database, store, resolved, cancellationToken)
                : null;

            return new PortalMoment(null, liveMatchCount, null, liveMatch);
        }

        // A champion is only ever answered for a competition with exactly
        // one category. Two categories both finishing means two champions,
        // and a cover naming one of them as "the" champion would be wrong
        // about the other — safer to say nothing than to pick.
        if (status == CompetitionState.Finished && categories.Count == 1)
        {
            var champion = await ResolveChampionAsync(
                database, store, resolved, categories[0].Id, cancellationToken);

            return new PortalMoment(null, 0, champion, null);
        }

        return new PortalMoment(null, 0, null, null);
    }

    /// <summary>
    /// The one category's champion, read two different ways depending on how
    /// it was actually decided.
    /// </summary>
    /// <remarks>
    /// A category that was ever promoted to a knockout is decided by that
    /// knockout's final, not by the group table it grew out of — the same
    /// distinction <c>AdvanceBracket</c> and <c>StandingsQuery</c> both make
    /// by <c>Phase</c>. Answered from the final only when the last round
    /// really is one match with a result <see cref="MatchWinner"/> can read;
    /// a knockout mid-final round, or one somehow left level, answers null
    /// rather than guess.
    ///
    /// Failing that, the category was decided by its table instead — league,
    /// or a group stage that was never promoted at all. Answered from
    /// position one only when there is exactly one table: real groups that
    /// never advanced to a knockout have one table per group, and none of
    /// them alone is the competition's champion.
    /// </remarks>
    private static async Task<ChampionSummary?> ResolveChampionAsync(
        SportFrogDbContext database,
        ObjectStore store,
        PublicCompetition resolved,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var bracket = await database.Matches
            .AsNoTracking()
            // A repechage ladder carries a phase too, but it decides a bronze
            // medal on its own, independent progression — not another round
            // of this one final. Left in, its matches could outrank the
            // actual final below and get read as though they were it.
            .Where(match => match.CategoryId == categoryId && match.Phase != null && !match.IsRepechage)
            .OrderByDescending(match => match.RoundNumber)
            .Select(match => new
            {
                match.RoundNumber,
                match.HomeTeamId,
                HomeTeamName = match.HomeTeam!.Name,
                HomeLogoKey = match.HomeTeam.Club!.LogoUrl,
                match.AwayTeamId,
                AwayTeamName = match.AwayTeam!.Name,
                AwayLogoKey = match.AwayTeam.Club!.LogoUrl,
                match.HomeTotal,
                match.AwayTotal,
                match.WalkoverTeamId,
                match.PenaltyHomeScore,
                match.PenaltyAwayScore,
            })
            .ToListAsync(cancellationToken);

        if (bracket.Count > 0)
        {
            var lastRound = bracket[0].RoundNumber;
            var finalists = bracket.Where(match => match.RoundNumber == lastRound).ToList();

            if (finalists.Count != 1)
            {
                // The last round drawn still has more than one tie in it —
                // not actually a final yet, whatever the competition's own
                // status says.
                return null;
            }

            var final = finalists[0];

            if (final.HomeTeamId is not { } homeId || final.AwayTeamId is not { } awayId)
            {
                // A knockout drawn in full books its final before it knows
                // who reaches it. Nobody to crown until the semifinals say so.
                return null;
            }

            var winner = MatchWinner.Resolve(
                homeId,
                awayId,
                final.WalkoverTeamId,
                final.HomeTotal,
                final.AwayTotal,
                final.PenaltyHomeScore,
                final.PenaltyAwayScore);

            if (winner == homeId)
            {
                return new ChampionSummary(
                    final.HomeTeamName!, await LinkAsync(store, resolved.OrganizationId, final.HomeLogoKey, cancellationToken));
            }

            if (winner == awayId)
            {
                return new ChampionSummary(
                    final.AwayTeamName!, await LinkAsync(store, resolved.OrganizationId, final.AwayLogoKey, cancellationToken));
            }

            return null;
        }

        var standings = await StandingsQuery.ForCategoryAsync(database, categoryId, cancellationToken);
        var leader = standings?.Groups is [{ Rows: [{ Played: > 0 } top, ..] }] ? top : null;

        if (leader is null)
        {
            return null;
        }

        var logoUrl = await LinkAsync(
            store, resolved.OrganizationId, standings!.LogoKeys.GetValueOrDefault(leader.TeamId), cancellationToken);

        return new ChampionSummary(leader.TeamName, logoUrl);
    }

    /// <summary>
    /// The one match this competition has in progress right now, for the
    /// Live hero variant. Only ever called when there is exactly one — see
    /// the caller in <see cref="MomentAsync"/> — the same unambiguous-only
    /// rule <see cref="ResolveChampionAsync"/> already follows for the
    /// champion.
    /// </summary>
    /// <remarks>
    /// The score is computed with <see cref="LiveScore.Compute"/>, the same
    /// engine <c>ReadMatches</c> and <c>ReadPublicCalendar</c> already use
    /// and already test on their own — not reimplemented a third time here.
    /// Gated on <see cref="ScoreMode.Cumulative"/> for the same reason
    /// <c>ReadPublicCalendar.LiveTotalsAsync</c> is: a sport decided by sets
    /// or by judges has no running tally to show mid-match, so the teams are
    /// named and the score stays null instead of showing a false 0-0.
    /// </remarks>
    private static async Task<LiveMatchSummary?> ResolveLiveMatchAsync(
        SportFrogDbContext database,
        ObjectStore store,
        PublicCompetition resolved,
        CancellationToken cancellationToken)
    {
        var match = await database.Matches
            .AsNoTracking()
            .Where(candidate => candidate.CompetitionId == resolved.CompetitionId
                && candidate.Status == MatchState.InProgress)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.HomeTeamId,
                HomeTeamName = candidate.HomeTeam!.Name,
                HomeLogoKey = candidate.HomeTeam.Club!.LogoUrl,
                candidate.AwayTeamId,
                AwayTeamName = candidate.AwayTeam!.Name,
                AwayLogoKey = candidate.AwayTeam.Club!.LogoUrl,
                CategoryName = candidate.Category!.Name,
                ScoreMode = candidate.Competition!.Sport!.ScoreMode,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (match is null)
        {
            return null;
        }

        int? homeScore = null;
        int? awayScore = null;

        if (match.ScoreMode == ScoreMode.Cumulative)
        {
            var events = await database.PlayerEvents
                .AsNoTracking()
                .Where(recorded => recorded.MatchId == match.Id && recorded.Metric!.AffectsScore)
                .Select(recorded => new
                {
                    TeamId = recorded.RosterEntry!.TeamId,
                    recorded.Metric!.ScorePoints,
                    recorded.Metric.CountsForOpponent,
                    recorded.Quantity,
                })
                .ToListAsync(cancellationToken);

            // In progress implies both teams are already named.
            var totals = LiveScore.Compute(
                events.Select(recorded => new ScoringEvent(
                    recorded.TeamId, recorded.ScorePoints, recorded.CountsForOpponent, recorded.Quantity)),
                match.HomeTeamId!.Value,
                match.AwayTeamId!.Value);

            homeScore = totals.Home;
            awayScore = totals.Away;
        }

        return new LiveMatchSummary(
            match.CategoryName,
            match.HomeTeamName,
            await LinkAsync(store, resolved.OrganizationId, match.HomeLogoKey, cancellationToken),
            match.AwayTeamName,
            await LinkAsync(store, resolved.OrganizationId, match.AwayLogoKey, cancellationToken),
            homeScore,
            awayScore);
    }

    /// <summary>A picture's signed link, or null for a key that was never set.</summary>
    private static Task<string?> LinkAsync(
        ObjectStore store, Guid organizationId, string? key, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(key)
            ? Task.FromResult<string?>(null)
            : store.ReadLinkAsync(organizationId, key, cancellationToken);
}

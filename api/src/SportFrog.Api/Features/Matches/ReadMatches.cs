using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Reads a calendar: a whole competition's, one category's, or one fixture.
/// </summary>
public static class ReadMatches
{
    public sealed record Summary(
        Guid Id,
        Guid CompetitionId,
        Guid CategoryId,
        string CategoryName,

        /// <summary>
        /// Null for a knockout slot drawn in full whose side is still
        /// "whoever wins another match" — see <see cref="HomePlaceholder"/>
        /// for what to show instead.
        /// </summary>
        Guid? HomeTeamId,
        string? HomeTeamName,

        /// <summary>
        /// "Ganador de {phase}", set exactly when <see cref="HomeTeamId"/> is
        /// not — the fixture whose winner still has to fill this side.
        /// </summary>
        string? HomePlaceholder,
        Guid? AwayTeamId,
        string? AwayTeamName,

        /// <summary>See <see cref="HomePlaceholder"/>; the same story, the other side.</summary>
        string? AwayPlaceholder,
        Guid? VenueSpaceId,
        string? VenueName,
        string? SpaceName,
        DateTimeOffset? ScheduledAt,
        short? RoundNumber,
        string? Phase,
        MatchState Status,
        Guid? WalkoverTeamId,
        IReadOnlyList<PeriodScore>? PeriodScores,
        int? HomeTotal,
        int? AwayTotal,

        /// <summary>The shootout that broke a level knockout match, once it needed one.</summary>
        short? PenaltyHomeScore,
        short? PenaltyAwayScore,

        /// <summary>
        /// The score read from events recorded so far, while the match is
        /// still being played. Set only when <see cref="Status"/> is
        /// <see cref="MatchState.InProgress"/> — null before kickoff, and
        /// null again once <see cref="HomeTotal"/> is the real thing.
        /// </summary>
        int? LiveHomeTotal,
        int? LiveAwayTotal,
        string? Notes,

        /// <summary>
        /// Whether this fixture belongs to a repechage ladder rather than the
        /// category's own run at the title — see <c>Repechage</c>.
        /// </summary>
        bool IsRepechage = false,

        /// <summary>Which of the final's two halves this repechage match settles the bronze for. See <c>Repechage</c>.</summary>
        short? RepechageBranch = null);

    public static IEndpointRouteBuilder MapReadMatches(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/competitions/{competitionId:guid}/matches", ListForCompetitionAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadMatches))
            .WithSummary("Reads the calendar of a competition.");

        routes.MapGet("/categories/{categoryId:guid}/matches", ListForCategoryAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadCategoryMatches")
            .WithSummary("Reads the calendar of a category.");

        routes.MapGet("/matches/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadMatch")
            .WithSummary("Reads one fixture.");

        routes.MapGet("/competitions/{competitionId:guid}/fixture.pdf", HandleCompetitionPdfAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadMatches) + "CompetitionPdf")
            .WithSummary("Renders a competition's calendar as a PDF.");

        routes.MapGet("/categories/{categoryId:guid}/fixture.pdf", HandleCategoryPdfAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadMatches) + "CategoryPdf")
            .WithSummary("Renders a category's calendar as a PDF.");

        return routes;
    }

    /// <summary>
    /// Every fixture of a competition, across its categories.
    /// </summary>
    /// <remarks>
    /// This is the view an organizer actually works from — a Sunday has
    /// matches of four divisions on three pitches, and reading it category by
    /// category would hide exactly the collisions the organizer is looking
    /// for.
    /// </remarks>
    private static async Task<IResult> ListForCompetitionAsync(
        Guid competitionId,
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        string? status = null,
        Guid? categoryId = null,
        short? round = null)
    {
        if (!await database.Competitions.AnyAsync(
                competition => competition.Id == competitionId, cancellationToken))
        {
            return Results.NotFound();
        }

        if (!TryReadStatus(status, out var state, out var refusal))
        {
            return refusal;
        }

        var matches = await Ordered(database.Matches
                .Where(match => match.CompetitionId == competitionId)
                .Where(match => categoryId == null || match.CategoryId == categoryId)
                .Where(match => round == null || match.RoundNumber == round)
                .Where(match => state == null || match.Status == state))
            .ToListAsync(cancellationToken);

        return Results.Ok(await WithLiveScoresAsync(database, matches, cancellationToken));
    }

    private static async Task<IResult> ListForCategoryAsync(
        Guid categoryId,
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        string? status = null,
        short? round = null)
    {
        if (!await database.Categories.AnyAsync(
                category => category.Id == categoryId, cancellationToken))
        {
            return Results.NotFound();
        }

        if (!TryReadStatus(status, out var state, out var refusal))
        {
            return refusal;
        }

        var matches = await Ordered(database.Matches
                .Where(match => match.CategoryId == categoryId)
                .Where(match => round == null || match.RoundNumber == round)
                .Where(match => state == null || match.Status == state))
            .ToListAsync(cancellationToken);

        return Results.Ok(await WithLiveScoresAsync(database, matches, cancellationToken));
    }

    private static async Task<IResult> HandleCompetitionPdfAsync(
        Guid competitionId,
        SportFrogDbContext database,
        ObjectStore store,
        CancellationToken cancellationToken,
        string? status = null,
        Guid? categoryId = null,
        short? round = null)
    {
        // Loaded whole, not projected: Settings is a jsonb column read back as
        // a real CompetitionSettings object once the row is materialized —
        // EF cannot translate a path into it, the way it can an ordinary
        // column, inside a Select.
        var competition = await database.Competitions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return Results.NotFound();
        }

        var competitionName = competition.Name;

        var branding = await CompetitionBranding.FromAsync(store, competition, cancellationToken);

        if (!TryReadStatus(status, out var state, out var refusal))
        {
            return refusal;
        }

        // A category named by id gets the same one-category heading a
        // request straight to /categories/{id}/fixture.pdf would — the query
        // parameter is for narrowing an otherwise whole-competition sheet to
        // one division without a second address to remember.
        var categoryName = categoryId is { } id
            ? await database.Categories
                .AsNoTracking()
                .Where(category => category.Id == id && category.CompetitionId == competitionId)
                .Select(category => category.Name)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var matches = await Ordered(database.Matches
                .Where(match => match.CompetitionId == competitionId)
                .Where(match => categoryId == null || match.CategoryId == categoryId)
                .Where(match => round == null || match.RoundNumber == round)
                .Where(match => state == null || match.Status == state))
            .ToListAsync(cancellationToken);

        if (RefuseIfIncomplete(round, matches) is { } refusalIncomplete)
        {
            return refusalIncomplete;
        }

        return Results.File(
            FixturePdf.Render(competitionName, categoryName, round, branding, matches), "application/pdf", "fixture.pdf");
    }

    private static async Task<IResult> HandleCategoryPdfAsync(
        Guid categoryId,
        SportFrogDbContext database,
        ObjectStore store,
        CancellationToken cancellationToken,
        string? status = null,
        short? round = null)
    {
        var category = await database.Categories
            .AsNoTracking()
            .Include(candidate => candidate.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category?.Competition is not { } competition)
        {
            return Results.NotFound();
        }

        var branding = await CompetitionBranding.FromAsync(store, competition, cancellationToken);

        if (!TryReadStatus(status, out var state, out var refusal))
        {
            return refusal;
        }

        var matches = await Ordered(database.Matches
                .Where(match => match.CategoryId == categoryId)
                .Where(match => round == null || match.RoundNumber == round)
                .Where(match => state == null || match.Status == state))
            .ToListAsync(cancellationToken);

        if (RefuseIfIncomplete(round, matches) is { } refusalIncomplete)
        {
            return refusalIncomplete;
        }

        return Results.File(
            FixturePdf.Render(competition.Name, category.Name, round, branding, matches), "application/pdf", "fixture.pdf");
    }

    /// <summary>
    /// Refuses a "whole sheet" request — no single round asked for — that
    /// would still print a gap. A single jornada is exempt: a match that did
    /// not fit when the calendar was generated still deserves a PDF of
    /// whatever the rest of that jornada does have.
    /// </summary>
    /// <remarks>
    /// A cancelled match carrying no date is expected, not a gap — it owes
    /// nothing further, per its own remark on <see cref="MatchState.Cancelled"/> —
    /// so it is the one status excluded from the check.
    /// </remarks>
    internal static IResult? RefuseIfIncomplete(short? round, IReadOnlyList<Summary> matches) =>
        round is null && matches.Any(match => match.ScheduledAt is null && match.Status != MatchState.Cancelled)
            ? Results.Problem(
                detail: "Todavía hay partidos sin fecha. Programalos todos antes de descargar el fixture " +
                        "completo, o generá el PDF de una jornada puntual en su lugar.",
                statusCode: StatusCodes.Status409Conflict)
            : null;

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var match = await Project(database.Matches.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        if (match is null)
        {
            return Results.NotFound();
        }

        var withLiveScore = await WithLiveScoresAsync(database, [match], cancellationToken);

        return Results.Ok(withLiveScore[0]);
    }

    /// <summary>
    /// Fills in <see cref="Summary.LiveHomeTotal"/> and
    /// <see cref="Summary.LiveAwayTotal"/> for whichever of these matches are
    /// in progress.
    /// </summary>
    /// <remarks>
    /// A second query rather than a correlated subquery in <see cref="Project"/>:
    /// the tally itself — an own goal scored against its own side, a
    /// three-pointer worth three — is domain logic that belongs in
    /// <see cref="LiveScore"/> and is tested there, not re-derived as SQL that
    /// nothing exercises directly. The extra round trip costs nothing a
    /// calendar page notices; the list of matches on it is never large.
    /// </remarks>
    private static async Task<List<Summary>> WithLiveScoresAsync(
        SportFrogDbContext database,
        List<Summary> matches,
        CancellationToken cancellationToken)
    {
        var inProgressIds = matches
            .Where(match => match.Status == MatchState.InProgress)
            .Select(match => match.Id)
            .ToList();

        if (inProgressIds.Count == 0)
        {
            return matches;
        }

        // A live score only exists for a sport that derives one from events
        // at all — see LiveScore.AppliesTo, which states the same rule this
        // mirrors. Not called directly: EF Core cannot translate a call into
        // it, and asking for the rest would not crash anyway — Compute would
        // return zero for every one of them, which is not a live score, it
        // is what "nothing to tally" looks like.
        var liveIds = await database.Matches
            .AsNoTracking()
            .Where(match => inProgressIds.Contains(match.Id))
            .Where(match => match.Competition!.Sport!.ScoreMode == ScoreMode.Cumulative)
            .Select(match => match.Id)
            .ToListAsync(cancellationToken);

        if (liveIds.Count == 0)
        {
            return matches;
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

        return [.. matches.Select(match =>
        {
            if (!liveIds.Contains(match.Id))
            {
                return match;
            }

            // In progress implies both teams are already named — starting a
            // match without them is refused before it ever reaches here.
            var totals = LiveScore.Compute(
                byMatch[match.Id].Select(recorded => new ScoringEvent(
                    recorded.TeamId, recorded.ScorePoints, recorded.CountsForOpponent, recorded.Quantity)),
                match.HomeTeamId!.Value,
                match.AwayTeamId!.Value);

            return match with { LiveHomeTotal = totals.Home, LiveAwayTotal = totals.Away };
        })];
    }

    /// <summary>
    /// A query parameter reaches no validator, so an unrecognized state is
    /// answered here in the same shape a bad field in a body would be.
    /// </summary>
    private static bool TryReadStatus(
        string? status,
        out MatchState? state,
        out IResult refusal)
    {
        state = null;
        refusal = Results.Empty;

        if (QueryFilter.OrAbsent(status) is not { } requested)
        {
            return true;
        }

        if (!WireEnum.TryParse<MatchState>(requested, out var parsed))
        {
            refusal = Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = [$"Unknown state. Available: {WireEnum.Options<MatchState>()}."],
            });

            return false;
        }

        state = parsed;
        return true;
    }

    /// <summary>
    /// Chronological, with the undated last.
    /// </summary>
    /// <remarks>
    /// A fixture with no date is drawn but not placed, and it belongs at the
    /// end of a calendar rather than at the beginning — which is where a plain
    /// ascending sort would put it, nulls first being the default for
    /// descending order and a trap either way.
    ///
    /// Among undated fixtures the round is the tie-break, but round numbers
    /// are only comparable within one stage: a category promoted out of a
    /// group stage restarts its bracket at round one, same as the group
    /// stage itself did, so "round one" alone cannot tell a group's first
    /// jornada from the knockout's first round. Phase is what can — it is
    /// null for every group match and set for every knockout one — so it is
    /// asked first, and round number only decides an order within whichever
    /// of the two a fixture belongs to.
    /// </remarks>
    private static IQueryable<Summary> Ordered(IQueryable<Match> matches) =>
        Project(matches
            .OrderBy(match => match.ScheduledAt == null)
            .ThenBy(match => match.ScheduledAt)
            .ThenBy(match => match.Phase != null)
            .ThenBy(match => match.RoundNumber));

    /// <summary>
    /// Shared so every calendar describes a fixture the same way.
    /// </summary>
    /// <remarks>
    /// The names travel with the identifiers because a calendar is read by a
    /// person: "Norte vs Sur, Cancha 1" is the answer, and making the caller
    /// resolve four identifiers to render one row would be four requests per
    /// match.
    /// </remarks>
    private static IQueryable<Summary> Project(IQueryable<Match> matches) =>
        matches.Select(match => new Summary(
            match.Id,
            match.CompetitionId,
            match.CategoryId,
            match.Category!.Name,
            match.HomeTeamId,
            match.HomeTeam!.Name,
            match.HomeTeamId == null && match.HomeSourceMatch != null
                ? "Ganador de " + match.HomeSourceMatch.Phase
                : null,
            match.AwayTeamId,
            match.AwayTeam!.Name,
            match.AwayTeamId == null && match.AwaySourceMatch != null
                ? "Ganador de " + match.AwaySourceMatch.Phase
                : null,
            match.VenueSpaceId,
            match.VenueSpace!.Venue!.Name,
            match.VenueSpace.Name,
            match.ScheduledAt,
            match.RoundNumber,
            match.Phase,
            match.Status,
            match.WalkoverTeamId,
            match.PeriodScores,
            match.HomeTotal,
            match.AwayTotal,
            match.PenaltyHomeScore,
            match.PenaltyAwayScore,
            null,
            null,
            match.Notes,
            match.IsRepechage,
            match.RepechageBranch));
}

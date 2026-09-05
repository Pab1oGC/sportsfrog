using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
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
        Guid HomeTeamId,
        string HomeTeamName,
        Guid AwayTeamId,
        string AwayTeamName,
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
        string? Notes);

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

            var totals = LiveScore.Compute(
                byMatch[match.Id].Select(recorded => new ScoringEvent(
                    recorded.TeamId, recorded.ScorePoints, recorded.CountsForOpponent, recorded.Quantity)),
                match.HomeTeamId,
                match.AwayTeamId);

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
            match.AwayTeamId,
            match.AwayTeam!.Name,
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
            match.Notes));
}

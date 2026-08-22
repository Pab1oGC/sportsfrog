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

        return Results.Ok(await Ordered(database.Matches
                .Where(match => match.CompetitionId == competitionId)
                .Where(match => categoryId == null || match.CategoryId == categoryId)
                .Where(match => round == null || match.RoundNumber == round)
                .Where(match => state == null || match.Status == state))
            .ToListAsync(cancellationToken));
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

        return Results.Ok(await Ordered(database.Matches
                .Where(match => match.CategoryId == categoryId)
                .Where(match => round == null || match.RoundNumber == round)
                .Where(match => state == null || match.Status == state))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var match = await Project(database.Matches.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return match is null ? Results.NotFound() : Results.Ok(match);
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
    /// descending order and a trap either way. The round is the tie-break,
    /// since an undated draw is still ordered by round.
    /// </remarks>
    private static IQueryable<Summary> Ordered(IQueryable<Match> matches) =>
        Project(matches
            .OrderBy(match => match.ScheduledAt == null)
            .ThenBy(match => match.ScheduledAt)
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
            match.Notes));
}

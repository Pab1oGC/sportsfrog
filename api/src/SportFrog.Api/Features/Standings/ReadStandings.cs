using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Standings;

/// <summary>
/// The table of a category.
/// </summary>
/// <remarks>
/// Computed on every read rather than kept in a column, and the schema agrees
/// — there is no standings table. A stored table is a second copy of the
/// truth that has to be rebuilt whenever a result is corrected, a match is
/// awarded, a team withdraws or a ruleset is renamed, and the day one of
/// those forgets to rebuild it the competition is publishing a lie. A
/// category holds a handful of teams and a season of matches; adding them up
/// is cheaper than keeping them honest.
/// </remarks>
public static class ReadStandings
{
    /// <param name="Position">
    /// Where the team stands, counting from one within its group. Answered
    /// here because it is what a table is read for, and because a client that
    /// numbered the rows itself would get it wrong the moment two teams are
    /// level and the order is not the ranking.
    /// </param>
    public sealed record Row(
        int Position,
        Guid TeamId,
        string TeamName,
        int Played,
        int Won,
        int Drawn,
        int Lost,
        int ScoreFor,
        int ScoreAgainst,
        int ScoreDifference,
        int Points);

    public sealed record Group(string? Label, IReadOnlyList<Row> Rows);

    /// <param name="Tiebreakers">
    /// The criteria applied, in the order they were applied. Returned so a
    /// table can explain itself: "why is Sur above Norte" is the most asked
    /// question about this object, and the answer is in the ruleset rather
    /// than in the numbers on screen.
    /// </param>
    public sealed record Response(
        Guid CategoryId,
        string CategoryName,
        string SportCode,
        Guid RulesetId,
        string RulesetName,
        IReadOnlyList<string> Tiebreakers,
        IReadOnlyList<Group> Groups);

    public static IEndpointRouteBuilder MapReadStandings(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/categories/{categoryId:guid}/standings", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadStandings))
            .WithSummary("Builds the standings table of a category.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories
            .AsNoTracking()
            .Where(candidate => candidate.Id == categoryId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                candidate.Competition!.SportCode,
                RulesetId = candidate.RulesetId ?? candidate.Competition.RulesetId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        var sport = await database.Sports
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Code == category.SportCode, cancellationToken);

        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == category.RulesetId, cancellationToken);

        if (sport is null || ruleset is null)
        {
            return Results.Problem(
                detail: "The rules for this category cannot be read, so its table cannot be built.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Every team entered, including one that withdrew: it keeps what it
        // played and keeps its place in the table, which is exactly why
        // withdrawing is a flag and not a deletion.
        var contenders = await database.Teams
            .AsNoTracking()
            .Where(team => team.CategoryId == categoryId)
            .Select(team => new Contender(team.Id, team.Name, team.GroupLabel))
            .ToListAsync(cancellationToken);

        // A match counts once it has a result. Finished is the ordinary way;
        // a walkover is the other, and it counts for the same reason it is a
        // state of its own — nobody played it, but it stands.
        var played = await database.Matches
            .AsNoTracking()
            .Where(match => match.CategoryId == categoryId)
            .Where(match => match.Status == MatchState.Finished
                || match.Status == MatchState.Walkover)
            .Where(match => match.HomeTotal != null && match.AwayTotal != null)
            .Select(match => new PlayedMatch(
                match.HomeTeamId, match.AwayTeamId, match.HomeTotal!.Value, match.AwayTotal!.Value))
            .ToListAsync(cancellationToken);

        var groups = StandingsCalculator.Build(contenders, played, sport.ScoreMode, ruleset.Config);

        return Results.Ok(new Response(
            category.Id,
            category.Name,
            category.SportCode,
            ruleset.Id,
            ruleset.Name,
            ruleset.Config.Tiebreakers,
            [.. groups.Select(group => new Group(
                group.Label,
                [.. group.Rows.Select((row, index) => new Row(
                    index + 1,
                    row.TeamId,
                    row.TeamName,
                    row.Played,
                    row.Won,
                    row.Drawn,
                    row.Lost,
                    row.ScoreFor,
                    row.ScoreAgainst,
                    row.ScoreDifference,
                    row.Points))]))]));
    }
}

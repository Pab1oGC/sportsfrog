using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Statistics;

/// <summary>
/// Who leads a category in each of the things it records.
/// </summary>
/// <remarks>
/// Derived on every read, like the table and for the same reason: a stored
/// leaderboard is a second copy of the truth that goes stale the moment an
/// event is corrected, and nothing would say so.
///
/// One board per metric rather than one number per player, because that is
/// what a competition publishes — top scorer, most cards — and because the
/// metrics of five sports do not form a single shape anybody could tabulate
/// together.
/// </remarks>
public static class ReadLeaders
{
    public sealed record Leader(
        int Position,
        Guid RosterEntryId,
        Guid AthleteId,
        string FirstName,
        string LastName,
        short? JerseyNumber,
        Guid TeamId,
        string TeamName,
        int Total);

    /// <param name="AffectsScore">
    /// Whether this metric moves the match score. Carried so a client can
    /// tell the scoring boards from the disciplinary ones without keeping its
    /// own list of codes.
    /// </param>
    public sealed record Board(
        Guid MetricId,
        string MetricCode,
        string MetricLabel,
        bool AffectsScore,
        IReadOnlyList<Leader> Leaders);

    public sealed record Response(
        Guid CategoryId,
        string CategoryName,
        string SportCode,
        IReadOnlyList<Board> Boards);

    public static IEndpointRouteBuilder MapReadLeaders(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/categories/{categoryId:guid}/leaders", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadLeaders))
            .WithSummary("Ranks the players of a category in each recorded metric.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        int top = 10)
    {
        if (top is < 1 or > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["top"] = ["A board shows between 1 and 100 places."],
            });
        }

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

        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == category.RulesetId, cancellationToken);

        if (ruleset is null)
        {
            return Results.Problem(
                detail: "The rules for this category cannot be read, so its boards cannot be built.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Which metrics get a board at all: the sport's rankable ones, minus
        // whatever this ruleset chose not to record. A league that does not
        // track assists should not publish an empty assists board — it does
        // not have one.
        var metrics = await database.SportMetrics
            .AsNoTracking()
            .Where(metric => metric.SportCode == category.SportCode && metric.IsRankable)
            .OrderBy(metric => metric.DisplayOrder)
            .Select(metric => new
            {
                metric.Id,
                metric.Code,
                metric.Label,
                metric.AffectsScore,
            })
            .ToListAsync(cancellationToken);

        if (ruleset.Config.Metrics is { } enabled)
        {
            metrics = [.. metrics.Where(metric => enabled.Contains(metric.Code, StringComparer.Ordinal))];
        }

        var tallies = await TallyAsync(categoryId, cancellationToken, database);

        var boards = metrics.Select(metric => new Board(
            metric.Id,
            metric.Code,
            metric.Label,
            metric.AffectsScore,

            // A metric nobody has recorded yet still gets its board, empty.
            // Early in a season that is the honest answer, and a missing board
            // reads as a metric the competition does not track.
            [.. Leaderboard
                .Rank(tallies.Where(tally => tally.MetricId == metric.Id), top)
                .Select(entry => new Leader(
                    entry.Position,
                    entry.Player.RosterEntryId,
                    entry.Player.AthleteId,
                    entry.Player.FirstName,
                    entry.Player.LastName,
                    entry.Player.JerseyNumber,
                    entry.Player.TeamId,
                    entry.Player.TeamName,
                    entry.Player.Total))]));

        return Results.Ok(new Response(
            category.Id, category.Name, category.SportCode, [.. boards]));
    }

    /// <summary>
    /// Every player's total of every metric, added up by the database.
    /// </summary>
    /// <remarks>
    /// One query for the whole category rather than one per board: a season
    /// of events is a lot of rows to move, and grouping them here would move
    /// all of them to add up numbers the database can add up in place.
    ///
    /// Only finished matches count, which is the same rule the table uses. A
    /// match still being played has a score that is not final and events that
    /// may yet be corrected; one that was cancelled after starting did not
    /// happen at all, and its events would otherwise leave a goal in the
    /// scorer list for a match nobody played.
    /// </remarks>
    private static async Task<List<Tally>> TallyAsync(
        Guid categoryId,
        CancellationToken cancellationToken,
        SportFrogDbContext database) =>
        await database.PlayerEvents
            .AsNoTracking()
            .Where(recorded => recorded.Match!.CategoryId == categoryId)
            .Where(recorded => recorded.Match!.Status == MatchState.Finished)
            .GroupBy(recorded => new
            {
                recorded.MetricId,
                recorded.RosterEntryId,
                recorded.RosterEntry!.AthleteId,
                recorded.RosterEntry.Athlete!.FirstName,
                recorded.RosterEntry.Athlete.LastName,
                recorded.RosterEntry.JerseyNumber,
                recorded.RosterEntry.TeamId,
                TeamName = recorded.RosterEntry.Team!.Name,
            })
            .Select(group => new Tally(
                group.Key.MetricId,
                group.Key.RosterEntryId,
                group.Key.AthleteId,
                group.Key.FirstName,
                group.Key.LastName,
                group.Key.JerseyNumber,
                group.Key.TeamId,
                group.Key.TeamName,
                group.Sum(recorded => recorded.Quantity)))
            .ToListAsync(cancellationToken);
}

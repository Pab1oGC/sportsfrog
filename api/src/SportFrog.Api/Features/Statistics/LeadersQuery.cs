using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Statistics;

/// <summary>
/// One board, ranked — one metric's, or the combined points board.
/// </summary>
/// <param name="MetricId">
/// Null for the points board once more than one scoring metric feeds it:
/// nothing single identifies a board that is free throws, field goals and
/// three-pointers added up together.
/// </param>
internal sealed record RankedBoard(
    Guid? MetricId,
    string MetricCode,
    string MetricLabel,
    bool AffectsScore,
    IReadOnlyList<(int Position, Tally Player)> Leaders);

/// <summary>Every board of a category.</summary>
internal sealed record LeadersResult(
    Guid CategoryId,
    string CategoryName,
    string SportCode,
    IReadOnlyList<RankedBoard> Boards);

/// <summary>
/// Reads what the boards are made of and ranks them.
/// </summary>
/// <remarks>
/// Shared with the public reading for the same reason the table is: two
/// implementations of a top scorer list is two lists, and a league that
/// publishes one number internally and another on its page has a problem no
/// amount of explaining fixes.
/// </remarks>
internal static class LeadersQuery
{
    public static async Task<LeadersResult?> ForCategoryAsync(
        SportFrogDbContext database,
        Guid categoryId,
        int top,
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
            return null;
        }

        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == category.RulesetId, cancellationToken);

        if (ruleset is null)
        {
            return null;
        }

        // Which metrics get a board at all: the sport's rankable ones, minus
        // whatever this ruleset chose not to record. A league that does not
        // track assists should not publish an empty assists board — it does
        // not have one.
        var metrics = await database.SportMetrics
            .AsNoTracking()
            .Where(metric => metric.SportCode == category.SportCode && metric.IsRankable)
            .OrderBy(metric => metric.DisplayOrder)
            .Select(metric => new { metric.Id, metric.Code, metric.Label, metric.AffectsScore, metric.ScorePoints })
            .ToListAsync(cancellationToken);

        if (ruleset.Config.Metrics is { } enabled)
        {
            metrics = [.. metrics.Where(metric =>
                enabled.Contains(metric.Code, StringComparer.Ordinal))];
        }

        var tallies = await TallyAsync(database, categoryId, cancellationToken);

        // A made three is not the same as a free throw, and ranking every
        // scoring metric on its own raw count would call them equal. Every
        // metric this ruleset tracks that moves the score is combined into
        // one board instead, weighted by what each is worth.
        //
        // Kept as its own board only once there is more than one: with a
        // single scoring metric — a goal, in every sport that has one today —
        // the weighted total and that metric's own board are the same
        // ranking, and its own name ("Gol") says more than a generic one
        // would.
        var scoring = metrics.Where(metric => metric.AffectsScore).ToList();
        var disciplinary = metrics.Where(metric => !metric.AffectsScore).ToList();

        var boards = new List<RankedBoard>();

        if (scoring.Count > 1)
        {
            var scoringIds = scoring.Select(metric => metric.Id).ToHashSet();
            var pointsPerMetric = scoring.ToDictionary(metric => metric.Id, metric => metric.ScorePoints);

            var combined = tallies
                .Where(tally => tally.MetricId is { } id && scoringIds.Contains(id))
                .GroupBy(tally => tally.RosterEntryId)
                .Select(group =>
                {
                    var player = group.First();
                    var total = group.Sum(tally => tally.Total * pointsPerMetric[tally.MetricId!.Value]);

                    return new Tally(
                        null, player.RosterEntryId, player.AthleteId, player.FirstName, player.LastName,
                        player.JerseyNumber, player.TeamId, player.TeamName, total);
                });

            boards.Add(new RankedBoard(null, "points", "Puntos", true, Leaderboard.Rank(combined, top)));
        }
        else if (scoring.Count == 1)
        {
            var metric = scoring[0];

            var weighted = tallies
                .Where(tally => tally.MetricId == metric.Id)
                .Select(tally => tally with { Total = tally.Total * metric.ScorePoints });

            boards.Add(new RankedBoard(metric.Id, metric.Code, metric.Label, true, Leaderboard.Rank(weighted, top)));
        }

        // A metric nobody has recorded yet still gets its board, empty. Early
        // in a season that is the honest answer, and a missing board reads as
        // a metric the competition does not track.
        boards.AddRange(disciplinary.Select(metric => new RankedBoard(
            metric.Id,
            metric.Code,
            metric.Label,
            metric.AffectsScore,
            Leaderboard.Rank(tallies.Where(tally => tally.MetricId == metric.Id), top))));

        return new LeadersResult(category.Id, category.Name, category.SportCode, boards);
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
        SportFrogDbContext database,
        Guid categoryId,
        CancellationToken cancellationToken) =>
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

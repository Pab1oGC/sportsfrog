using SportFrog.Api.Features.Rulebook;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Standings;

/// <summary>One finished match, reduced to what a table needs from it.</summary>
internal sealed record PlayedMatch(Guid HomeTeamId, Guid AwayTeamId, int HomeScore, int AwayScore);

/// <summary>A competitor, before anything has been counted.</summary>
internal sealed record Contender(Guid TeamId, string TeamName, string? GroupLabel);

/// <summary>What one team has done, and what it is worth.</summary>
internal sealed record StandingsRow
{
    public required Guid TeamId { get; init; }
    public required string TeamName { get; init; }
    public string? GroupLabel { get; init; }

    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int ScoreFor { get; set; }
    public int ScoreAgainst { get; set; }
    public int Points { get; set; }

    public int ScoreDifference => ScoreFor - ScoreAgainst;
}

/// <summary>
/// Builds a standings table out of results and a ruleset.
/// </summary>
/// <remarks>
/// No database underneath: matches and contenders go in, an ordered table
/// comes out. A table is the most argued-about object in a competition, so it
/// has to be something a person can follow line by line without a schema in
/// front of them.
///
/// This is where the rulebook module finally does something. Every outcome is
/// priced by looking its key up in the ruleset's <c>points</c>, using the same
/// key format the ruleset was validated against — which is why a volleyball
/// league can pay for losing 2-3 and a football one cannot, without a word of
/// sport-specific code here.
/// </remarks>
internal static class StandingsCalculator
{
    /// <summary>
    /// The table, or one per group where the draw has them.
    /// </summary>
    /// <remarks>
    /// Grouped because positions restart: the first of group A and the first
    /// of group B are both first, and a single list would make one of them
    /// fifth.
    /// </remarks>
    public static IReadOnlyList<StandingsGroup> Build(
        IReadOnlyList<Contender> contenders,
        IReadOnlyList<PlayedMatch> played,
        ScoreMode mode,
        RulesetConfiguration rules) =>
        [.. contenders
            .GroupBy(contender => contender.GroupLabel)
            .OrderBy(group => group.Key == null)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new StandingsGroup(
                group.Key,
                Rank(Tally(group.ToList(), played, mode, rules), played, mode, rules)))];

    /// <summary>
    /// Counts what happened, without deciding any order yet.
    /// </summary>
    /// <remarks>
    /// Every contender gets a row, including one that has played nothing: a
    /// team missing from the table because its first match is next week reads
    /// as a team that was thrown out.
    ///
    /// A match counts once it has a result, which is finished or awarded. The
    /// caller decides that — a walkover reaches here as an ordinary score,
    /// because in the table it is one.
    /// </remarks>
    private static List<StandingsRow> Tally(
        IReadOnlyList<Contender> contenders,
        IReadOnlyList<PlayedMatch> played,
        ScoreMode mode,
        RulesetConfiguration rules)
    {
        var rows = contenders.ToDictionary(
            contender => contender.TeamId,
            contender => new StandingsRow
            {
                TeamId = contender.TeamId,
                TeamName = contender.TeamName,
                GroupLabel = contender.GroupLabel,
            });

        foreach (var match in played)
        {
            // A match between a team of this group and one of another is not
            // this group's business, and in a well-drawn competition does not
            // exist. Skipped rather than half-counted.
            if (!rows.TryGetValue(match.HomeTeamId, out var home)
                || !rows.TryGetValue(match.AwayTeamId, out var away))
            {
                continue;
            }

            Record(home, match.HomeScore, match.AwayScore, mode, rules);
            Record(away, match.AwayScore, match.HomeScore, mode, rules);
        }

        return [.. rows.Values];
    }

    /// <summary>
    /// Adds one match to one side of it.
    /// </summary>
    private static void Record(
        StandingsRow row,
        int own,
        int against,
        ScoreMode mode,
        RulesetConfiguration rules)
    {
        row.Played++;
        row.ScoreFor += own;
        row.ScoreAgainst += against;

        if (own > against)
        {
            row.Won++;
        }
        else if (own == against)
        {
            row.Drawn++;
        }
        else
        {
            row.Lost++;
        }

        // An outcome the ruleset does not price is worth nothing. That cannot
        // happen for a ruleset this application wrote — the rulebook module
        // refuses to save one with a missing outcome — but a table must not
        // throw over a row somebody loaded another way.
        row.Points += rules.Points.TryGetValue(MatchOutcomes.For(mode, own, against), out var value)
            ? value
            : 0;
    }

    /// <summary>
    /// Puts the rows in order: points first, then whatever the ruleset says
    /// to look at next.
    /// </summary>
    /// <remarks>
    /// Teams level on points are handed to the tiebreakers as a group rather
    /// than compared in pairs, because one of the criteria — head-to-head —
    /// only means anything against a known set of rivals.
    /// </remarks>
    private static IReadOnlyList<StandingsRow> Rank(
        List<StandingsRow> rows,
        IReadOnlyList<PlayedMatch> played,
        ScoreMode mode,
        RulesetConfiguration rules) =>
        [.. rows
            .GroupBy(row => row.Points)
            .OrderByDescending(group => group.Key)
            .SelectMany(group => Tiebreaking.Resolve([.. group], played, mode, rules))];
}

/// <summary>One table: the whole category, or one group of it.</summary>
internal sealed record StandingsGroup(string? Label, IReadOnlyList<StandingsRow> Rows);

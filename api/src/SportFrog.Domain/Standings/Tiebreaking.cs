
namespace SportFrog.Domain.Standings;

/// <summary>
/// Separates teams that are level, using the criteria the ruleset lists and
/// in the order it lists them.
/// </summary>
/// <remarks>
/// Resolved group by group rather than with a comparer, and that is not a
/// stylistic choice. Head-to-head is not a property of a team: it is a
/// property of a team <em>within a set of tied teams</em>, and with three
/// level on points it is routinely circular — A beat B, B beat C, C beat A.
/// A pairwise comparison of a circle is not transitive, and handing a sort
/// something non-transitive produces an order that depends on which rows
/// happened to be compared first.
///
/// So each tied group is resolved on its own: the criterion is computed
/// against that group, the group is split by the result, and whatever is
/// still level goes to the next criterion. When the criteria run out, the
/// remainder is ordered by name — level on everything the rules mention is
/// still two rows, and which comes first has to be decided by something that
/// does not change between readings.
/// </remarks>
public static class Tiebreaking
{
    /// <summary>
    /// Orders one set of teams that are level on points.
    /// </summary>
    public static IReadOnlyList<StandingsRow> Resolve(
        IReadOnlyList<StandingsRow> tied,
        IReadOnlyList<PlayedMatch> played,
        ScoreMode mode,
        RulesetConfiguration rules,
        int criterion = 0)
    {
        if (tied.Count == 1 || criterion >= rules.Tiebreakers.Count)
        {
            return [.. tied.OrderBy(row => row.TeamName, StringComparer.Ordinal)];
        }

        var next = rules.Tiebreakers[criterion];

        // Computed once per row against this group, because head-to-head
        // changes meaning when the group does: the same team separated from a
        // different set of rivals has a different score here.
        var ranked = tied
            .Select(row => (Row: row, Key: Score(next, row, tied, played, mode, rules)))
            .GroupBy(entry => entry.Key)
            .OrderByDescending(group => group.Key);

        return
        [
            .. ranked.SelectMany(group => Resolve(
                [.. group.Select(entry => entry.Row)], played, mode, rules, criterion + 1)),
        ];
    }

    /// <summary>
    /// How well a team does on one criterion. Higher is always better, so a
    /// criterion where less is better is negated rather than given its own
    /// direction to keep track of.
    /// </summary>
    private static int Score(
        string criterion,
        StandingsRow row,
        IReadOnlyList<StandingsRow> tied,
        IReadOnlyList<PlayedMatch> played,
        ScoreMode mode,
        RulesetConfiguration rules) =>
        criterion switch
        {
            Tiebreaker.ScoreDifference => row.ScoreDifference,
            Tiebreaker.ScoreFor => row.ScoreFor,
            Tiebreaker.ScoreAgainst => -row.ScoreAgainst,
            Tiebreaker.Wins => row.Won,
            Tiebreaker.HeadToHead => HeadToHead(row, tied, played, mode, rules),

            // A criterion nobody knows how to compute separates nothing. It
            // cannot come from a ruleset this application wrote — the rulebook
            // module refuses unknown tiebreakers — and a table must not throw
            // over a row loaded another way.
            _ => 0,
        };

    /// <summary>
    /// Points taken from the other teams that are level, and only from them.
    /// </summary>
    /// <remarks>
    /// The mini-table everyone means by "head-to-head": the same ruleset
    /// applied to the same teams, over the matches they played against each
    /// other. Matches against anybody outside the tie are ignored, which is
    /// the whole point of the criterion — the rest of the season already
    /// produced the tie.
    /// </remarks>
    private static int HeadToHead(
        StandingsRow row,
        IReadOnlyList<StandingsRow> tied,
        IReadOnlyList<PlayedMatch> played,
        ScoreMode mode,
        RulesetConfiguration rules)
    {
        var group = tied.Select(other => other.TeamId).ToHashSet();

        var points = 0;

        foreach (var match in played)
        {
            // Both sides have to be in the tie. A match one of them played
            // against a team outside it is part of why they are level, not
            // part of what separates them.
            if (!group.Contains(match.HomeTeamId) || !group.Contains(match.AwayTeamId))
            {
                continue;
            }

            if (match.HomeTeamId == row.TeamId)
            {
                points += Value(match.HomeScore, match.AwayScore, mode, rules);
            }
            else if (match.AwayTeamId == row.TeamId)
            {
                points += Value(match.AwayScore, match.HomeScore, mode, rules);
            }
        }

        return points;
    }

    /// <summary>
    /// What one result is worth, priced by the same ruleset the table itself
    /// uses.
    /// </summary>
    private static int Value(int own, int against, ScoreMode mode, RulesetConfiguration rules) =>
        rules.Points.TryGetValue(MatchOutcomes.For(mode, own, against), out var value) ? value : 0;
}

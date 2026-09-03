using System.Globalization;
using SportFrog.Domain.Matches;

namespace SportFrog.Domain.Rules;

/// <summary>
/// Scoring for a match settled by periods won rather than points scored —
/// volleyball, badminton — where the side that takes the deciding period
/// wins, and the raw score inside a period is not the result of anything.
/// </summary>
/// <remarks>
/// Moved out of <see cref="ScoreConsolidation"/> and <see cref="MatchOutcomes"/>
/// as-is: this is the same arithmetic those two used to run inline behind a
/// mode check, now reachable on its own.
/// </remarks>
public sealed class SetsMatchOutcomeRules : IMatchOutcomeRules
{
    public (int Home, int Away) Consolidate(IReadOnlyList<PeriodScore> periods) =>
        (periods.Count(period => period.Home > period.Away),
         periods.Count(period => period.Away > period.Home));

    /// <remarks>
    /// Every distinct set score a best-of-<paramref name="periods"/> match
    /// can finish on, from both sides. Read as the scoreline of the team the
    /// price applies to: win_3_1 is the winner's three sets to one, loss_1_3
    /// the same match seen by the team that lost it.
    /// </remarks>
    public IReadOnlyCollection<string> RequiredOutcomes(short periods)
    {
        // The deciding set: best of five is won at three, best of three at
        // two. Periods are validated odd before this is reached, so there is
        // always one.
        var toWin = (periods + 1) / 2;

        var outcomes = new List<string>(toWin * 2);

        for (var lost = 0; lost < toWin; lost++)
        {
            outcomes.Add(Format("win", toWin, lost));
            outcomes.Add(Format("loss", lost, toWin));
        }

        return outcomes;
    }

    /// <remarks>
    /// A match played in sets runs until someone takes the deciding one, so
    /// there is no draw to price and offering one would be offering a result
    /// that cannot be recorded.
    /// </remarks>
    public IReadOnlyCollection<string> OptionalOutcomes() => [];

    public string OutcomeFor(int own, int against) => Format(own > against ? "win" : "loss", own, against);

    private static string Format(string result, int own, int against) =>
        string.Create(CultureInfo.InvariantCulture, $"{result}_{own}_{against}");
}

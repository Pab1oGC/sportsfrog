using System.Globalization;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// The ways a match of a given sport can end, which is the set of outcomes a
/// ruleset has to put a price on.
/// </summary>
/// <remarks>
/// Derived rather than listed. Under <see cref="ScoreMode.Cumulative"/> a
/// match is won, drawn or lost and there is nothing to derive. Under
/// <see cref="ScoreMode.Sets"/> the outcomes depend on how many periods are
/// played: best of five ends 3-0, 3-1 or 3-2, best of three ends 2-0 or 2-1,
/// and a league that awards a point for taking a set off the winner needs
/// those spelled out separately.
///
/// Writing the volleyball outcomes as a constant would work until the first
/// three-set sport, and wally is already in the catalog.
/// </remarks>
internal static class MatchOutcomes
{
    public const string Win = "win";
    public const string Draw = "draw";
    public const string Loss = "loss";

    /// <summary>
    /// Outcomes a ruleset for this sport must price.
    /// </summary>
    public static IReadOnlyCollection<string> RequiredFor(ScoreMode mode, short periods) =>
        mode == ScoreMode.Sets ? BySetScore(periods) : [Win, Loss];

    /// <summary>
    /// Which outcome a finished match was, seen from one side.
    /// </summary>
    /// <remarks>
    /// The other half of the same idea, and it belongs here rather than in
    /// the standings for one reason: this has to produce exactly the keys
    /// <see cref="RequiredFor"/> asked the ruleset to price. Written twice
    /// they would drift by a character, and a table would silently award zero
    /// for a result nobody had failed to price — the ruleset would look
    /// complete and the points would be wrong.
    /// </remarks>
    public static string For(ScoreMode mode, int own, int against) =>
        mode == ScoreMode.Sets
            ? Format(own > against ? "win" : "loss", own, against)
            : own > against ? Win
            : own == against ? Draw
            : Loss;

    /// <summary>
    /// Outcomes it may price, but need not.
    /// </summary>
    /// <remarks>
    /// Only the draw, and optional rather than required because whether a
    /// match can end level is decided by the competition and not by the
    /// sport. Basketball under FIBA plays overtime until someone wins, so
    /// those rulesets price a win and a loss and nothing else; a municipal
    /// league that stops at the final buzzer draws, and says so by putting a
    /// value on it. Requiring the key would force the first to answer a
    /// question it does not ask, and forbidding it would refuse the second a
    /// rule it really plays by.
    ///
    /// A sport played in sets is the one case where the sport does decide:
    /// the match runs until someone takes the deciding set, so there is no
    /// draw to price and offering one would be offering a result that cannot
    /// be recorded.
    /// </remarks>
    public static IReadOnlyCollection<string> OptionalFor(ScoreMode mode) =>
        mode == ScoreMode.Sets ? [] : [Draw];

    /// <summary>
    /// Every distinct set score a best-of-<paramref name="periods"/> match
    /// can finish on, from both sides.
    /// </summary>
    /// <remarks>
    /// Read as the scoreline of the team the price applies to: win_3_1 is the
    /// winner's three sets to one, loss_1_3 the same match seen by the team
    /// that lost it. Keeping both directions means a league can pay a losing
    /// side for taking sets without the standings code having to infer the
    /// mirror of a rule that was only half written.
    /// </remarks>
    private static IReadOnlyCollection<string> BySetScore(short periods)
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

    private static string Format(string result, int own, int against) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{result}_{own}_{against}");
}

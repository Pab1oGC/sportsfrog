using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// A match played in sets stops the moment one side has enough of them.
/// </summary>
/// <remarks>
/// Moved out of <see cref="ResultPolicy"/> as-is, except for how the
/// consolidated score is reached: through the same
/// <see cref="IMatchOutcomeRulesRegistry"/> a caller resolved through the
/// container would use, rather than the static
/// <see cref="ScoreConsolidation"/> facade.
/// </remarks>
internal sealed class SetsResultShape(IMatchOutcomeRulesRegistry outcomeRules) : IResultShapeRules
{
    /// <summary>
    /// How far ahead the side that reaches <see cref="SportFrog.Api.Infrastructure.Persistence.Entities.Sport.TargetPointsPerSet"/>
    /// still has to pull before the period is actually decided — FIVB's
    /// "win by two," the one shape every sport <c>TargetPointsPerSet</c>
    /// will ever describe shares. Not catalog data: see that property's own
    /// remarks on why a constant said this better than a second column only
    /// one value would ever go in.
    /// </summary>
    private const short SetWinMargin = 2;

    public ScoreMode Mode => ScoreMode.Sets;

    public void Inspect(MatchRules rules, IReadOnlyList<PeriodScore> periods, List<ResultViolation> violations)
    {
        var label = rules.Configuration.Periods.Label;

        if (periods.Any(period => period.Home == period.Away))
        {
            // Nothing decides a tied set, so a tied one was not finished.
            violations.Add(new ResultViolation(
                "PeriodScores", $"Un {label} no puede terminar empatado."));
            return;
        }

        // Not part of IMatchOutcomeRules: it never branches on mode, so
        // there is no strategy to resolve — it stays a plain call.
        var toWin = ScoreConsolidation.PeriodsToWin(rules.Configuration.Periods.Count);
        var (home, away) = outcomeRules.For(Mode).Consolidate(periods);
        var winner = Math.Max(home, away);
        var loser = Math.Min(home, away);

        if (winner != toWin)
        {
            violations.Add(new ResultViolation(
                "PeriodScores",
                winner < toWin
                    ? $"Ningún lado llegó a {PeriodLabel.Count(toWin, label)}, así que este " +
                      "partido no terminó. Aplazálo si se va a reanudar."
                    : $"Un partido se gana en {PeriodLabel.Count(toWin, label)}, y un lado tiene " +
                      $"{winner}. No se juega nada después del {label} decisivo."));
        }

        if (loser >= toWin)
        {
            violations.Add(new ResultViolation(
                "PeriodScores", $"Los dos lados no pueden llegar a {PeriodLabel.Count(toWin, label)}."));
        }

        InspectPointTargets(rules, periods, toWin, label, violations);
    }

    /// <summary>
    /// Whether each period actually reached the score it takes to decide
    /// one, for a sport where that is a question at all.
    /// </summary>
    /// <remarks>
    /// Everything above this reads the periods as a sequence of set wins —
    /// which side took each one, and whether that adds up to the match
    /// being over. None of it looks at the points inside a period, so a
    /// volleyball set reported 20-18 passes every check above exactly as
    /// well as a real 25-23 does. <see cref="SportFrog.Api.Infrastructure.Persistence.Entities.Sport.TargetPointsPerSet"/>
    /// is null for a sport that is not a race to a point total — a
    /// taekwondo asalto runs to the end of its clock, not to a score — so
    /// this does nothing there, same as before it existed.
    /// </remarks>
    private static void InspectPointTargets(
        MatchRules rules,
        IReadOnlyList<PeriodScore> periods,
        int toWin,
        string label,
        List<ResultViolation> violations)
    {
        if (rules.Sport.TargetPointsPerSet is not { } target)
        {
            return;
        }

        var decidingTarget = rules.Sport.TargetPointsDecidingSet ?? target;

        // The last period a best-of-(toWin * 2 - 1) match can reach — best
        // of five is won at three, so its fifth and last set is the one
        // that races to the deciding target instead of the regular one.
        var decidingPeriod = (short)(toWin * 2 - 1);

        foreach (var period in periods)
        {
            var periodTarget = period.Period == decidingPeriod ? decidingTarget : target;
            var winnerPoints = Math.Max(period.Home, period.Away);
            var loserPoints = Math.Min(period.Home, period.Away);

            if (winnerPoints >= periodTarget && winnerPoints - loserPoints >= SetWinMargin)
            {
                continue;
            }

            violations.Add(new ResultViolation(
                "PeriodScores",
                $"El {label} {period.Period} ({period.Home}-{period.Away}) no llega a {periodTarget} " +
                $"puntos con una diferencia de al menos {SetWinMargin}."));
        }
    }
}

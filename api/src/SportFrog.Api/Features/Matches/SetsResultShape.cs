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
    }
}

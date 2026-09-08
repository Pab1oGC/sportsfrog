using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// A judged bout is exactly one performance a side, and nothing decides a
/// tie between two judges' scores the way a deciding point does under a
/// running score — so a tied score is refused outright rather than reported
/// as an unfinished result.
/// </summary>
/// <remarks>
/// Sibling of <see cref="CumulativeResultShape"/> and <see cref="SetsResultShape"/>.
/// The period-count check is the same shape as the cumulative one — every
/// configured period is reported, and here there is only ever one to
/// configure — but a judged score also cannot end level, which is instead
/// exactly what <see cref="SetsResultShape"/> refuses about one set.
/// </remarks>
internal sealed class JudgedResultShape : IResultShapeRules
{
    public ScoreMode Mode => ScoreMode.Judged;

    public void Inspect(MatchRules rules, IReadOnlyList<PeriodScore> periods, List<ResultViolation> violations)
    {
        var expected = rules.Configuration.Periods.Count;
        var label = rules.Configuration.Periods.Label;

        if (periods.Count != expected)
        {
            violations.Add(new ResultViolation(
                "PeriodScores",
                $"{rules.Sport.Name} se decide en {PeriodLabel.Count(expected, label)}, y se " +
                $"reportaron {PeriodLabel.Count(periods.Count, label)}."));

            return;
        }

        if (periods.Any(period => period.Home == period.Away))
        {
            violations.Add(new ResultViolation(
                "PeriodScores",
                $"Un {label} no puede terminar empatado. Los jueces tienen que resolver el " +
                "empate antes de cargar el resultado."));
        }
    }
}

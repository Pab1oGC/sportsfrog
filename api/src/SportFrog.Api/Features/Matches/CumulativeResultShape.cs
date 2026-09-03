using SportFrog.Domain.Matches;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Under a cumulative score every period is played, so every period is
/// reported.
/// </summary>
/// <remarks>
/// Moved out of <see cref="ResultPolicy"/> as-is. A match abandoned halfway
/// is not a short result: it is postponed or cancelled, and those are states
/// rather than scores.
/// </remarks>
internal sealed class CumulativeResultShape : IResultShapeRules
{
    public void Inspect(MatchRules rules, IReadOnlyList<PeriodScore> periods, List<ResultViolation> violations)
    {
        var expected = rules.Configuration.Periods.Count;

        if (periods.Count != expected)
        {
            violations.Add(new ResultViolation(
                "PeriodScores",
                $"{rules.Sport.Name} se juega en " +
                $"{PeriodLabel.Count(expected, rules.Configuration.Periods.Label)} bajo estas " +
                $"reglas, y se reportaron " +
                $"{PeriodLabel.Count(periods.Count, rules.Configuration.Periods.Label)}."));
        }
    }
}

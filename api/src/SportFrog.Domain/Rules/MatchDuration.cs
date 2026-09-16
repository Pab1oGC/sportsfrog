namespace SportFrog.Domain.Rules;

/// <summary>
/// How long playing a whole match takes, for the sport a reglamento
/// describes.
/// </summary>
/// <remarks>
/// Computed from <see cref="PeriodRules.Minutes"/> where a clock says so;
/// read straight off <see cref="PeriodRules.EstimatedMinutes"/> where there
/// is no clock to compute from at all. Null only when neither is declared —
/// a reglamento with nothing to say about duration yet. Kept apart from
/// <see cref="PeriodRules"/> itself for the same reason
/// <see cref="MatchOutcomes"/> is kept apart from <see cref="RulesetConfiguration"/>
/// — a derivation belongs beside the record it reads, not inside it.
/// </remarks>
public static class MatchDuration
{
    public static short? From(PeriodRules periods) =>
        periods.Minutes is { } minutes
            ? (short)(periods.Count * minutes + Math.Max(0, periods.Count - 1) * (periods.BreakMinutes ?? 0))
            : periods.EstimatedMinutes;
}

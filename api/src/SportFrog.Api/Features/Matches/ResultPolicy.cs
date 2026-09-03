using SportFrog.Domain.Matches;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Whether a score could have happened in this sport, under these rules.
/// </summary>
/// <remarks>
/// The schema checks that a finished match has totals and an author. It has
/// no way to check that a volleyball match ended in three sets rather than
/// four and a half, or that a football match reports both halves — those
/// depend on the ruleset, which lives in a jsonb column two joins away.
///
/// Finding those rules is <see cref="MatchRulesLookup"/>'s job, not this
/// one's: this class never touches the database, and needs nothing more
/// than a <see cref="MatchRules"/> built by hand to be exercised.
///
/// The structural checks — numbering, negative scores — apply to every mode
/// and stay here. What is mode-specific is delegated to
/// <see cref="IResultShapeRules"/>, resolved through
/// <see cref="IResultShapeRulesRegistry"/> rather than a mode check, so a
/// mode this class does not already know is a new registration and not a
/// new branch.
/// </remarks>
internal sealed class ResultPolicy(IResultShapeRulesRegistry shapeRules)
{
    /// <summary>
    /// Everything wrong with the periods reported, or nothing.
    /// </summary>
    public IReadOnlyList<ResultViolation> Inspect(MatchRules rules, IReadOnlyList<PeriodScore> periods)
    {
        var violations = new List<ResultViolation>();

        if (periods.Count == 0)
        {
            return [new ResultViolation("PeriodScores", "Un resultado necesita al menos un período.")];
        }

        InspectNumbering(periods, violations);

        if (violations.Count > 0)
        {
            // Everything below reads the periods as a sequence. Judging a
            // sequence that is not one produces confident nonsense.
            return violations;
        }

        shapeRules.For(rules.Sport.ScoreMode).Inspect(rules, periods, violations);

        return violations;
    }

    /// <summary>
    /// The periods have to be one, two, three — each once.
    /// </summary>
    private static void InspectNumbering(
        IReadOnlyList<PeriodScore> periods,
        List<ResultViolation> violations)
    {
        var numbers = periods.Select(period => period.Period).ToList();

        if (numbers.Distinct().Count() != numbers.Count)
        {
            violations.Add(new ResultViolation(
                "PeriodScores", "Un período está reportado dos veces."));
        }
        else if (!numbers.Order().SequenceEqual(Enumerable.Range(1, numbers.Count).Select(n => (short)n)))
        {
            violations.Add(new ResultViolation(
                "PeriodScores",
                $"Los períodos deben numerarse del 1 al {numbers.Count} sin que falte ninguno."));
        }

        if (periods.Any(period => period.Home < 0 || period.Away < 0))
        {
            violations.Add(new ResultViolation(
                "PeriodScores", "El marcador de un período no puede ser negativo."));
        }
    }

    /// <summary>
    /// The score a walkover is recorded with, as the ruleset defines it.
    /// </summary>
    /// <remarks>
    /// Taken from the rules rather than from the request, because a walkover
    /// is awarded rather than played: what it is worth was decided when the
    /// competition was set up, and letting it be typed in per match would make
    /// two walkovers in one league worth different things.
    ///
    /// Static, unlike <see cref="Inspect"/>: nothing here depends on the
    /// score mode, so there is no rules object to resolve.
    /// </remarks>
    public static (int Winner, int Loser)? WalkoverScore(MatchRules rules) =>
        rules.Configuration.Walkover is { } walkover
            ? (walkover.WinnerScore, walkover.LoserScore)
            : null;
}

using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Matches;

/// <summary>The rules a match is played and read under.</summary>
/// <param name="Configuration">
/// The category's own ruleset where it has one, otherwise the competition's.
/// A category is allowed to vary the rules of its division, so the effective
/// one is the only one worth asking about.
/// </param>
internal sealed record MatchRules(Sport Sport, RulesetConfiguration Configuration);

/// <summary>Something a result says that its sport does not allow.</summary>
internal sealed record ResultViolation(string Property, string Message);

/// <summary>
/// Whether a score could have happened in this sport, under these rules.
/// </summary>
/// <remarks>
/// The schema checks that a finished match has totals and an author. It has
/// no way to check that a volleyball match ended in three sets rather than
/// four and a half, or that a football match reports both halves — those
/// depend on the ruleset, which lives in a jsonb column two joins away.
///
/// The structural checks — numbering, negative scores — apply to every mode
/// and stay here. What is mode-specific is delegated to
/// <see cref="IResultShapeRules"/>, one implementation per score mode, so a
/// mode this class does not already know is a new implementation of that
/// interface rather than a third branch added here.
/// </remarks>
internal sealed class ResultPolicy(SportFrogDbContext database)
{
    private static readonly IResultShapeRules CumulativeShape = new CumulativeResultShape();
    private static readonly IResultShapeRules SetsShape = new SetsResultShape();

    /// <summary>
    /// The sport and the effective ruleset behind a fixture.
    /// </summary>
    public async Task<MatchRules?> FindRulesAsync(Match match, CancellationToken cancellationToken)
    {
        var context = await database.Matches
            .AsNoTracking()
            .Where(candidate => candidate.Id == match.Id)
            .Select(candidate => new
            {
                candidate.Competition!.SportCode,

                // The override where the category sets one, and the
                // competition's otherwise. Resolved in the query so the
                // fallback is not a rule every caller has to remember.
                RulesetId = candidate.Category!.RulesetId ?? candidate.Competition.RulesetId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (context is null)
        {
            return null;
        }

        var sport = await database.Sports
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Code == context.SportCode, cancellationToken);

        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == context.RulesetId, cancellationToken);

        return sport is null || ruleset is null ? null : new MatchRules(sport, ruleset.Config);
    }

    /// <summary>
    /// Everything wrong with the periods reported, or nothing.
    /// </summary>
    public static IReadOnlyList<ResultViolation> Inspect(
        MatchRules rules,
        IReadOnlyList<PeriodScore> periods)
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

        var shape = rules.Sport.ScoreMode == ScoreMode.Sets ? SetsShape : CumulativeShape;
        shape.Inspect(rules, periods, violations);

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
    /// </remarks>
    public static (int Winner, int Loser)? WalkoverScore(MatchRules rules) =>
        rules.Configuration.Walkover is { } walkover
            ? (walkover.WinnerScore, walkover.LoserScore)
            : null;
}

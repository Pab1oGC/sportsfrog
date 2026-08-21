using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>Something a configuration says that its sport does not allow.</summary>
internal sealed record RulesetViolation(string Property, string Message);

/// <summary>
/// Decides whether a configuration makes sense for the sport it claims to be
/// for.
///
/// This is the layer the schema comment points at. The CHECK constraint on
/// <c>rulesets.config</c> only asserts that the three required keys are
/// present, which is as far as a constraint can go: it cannot know that a
/// volleyball ruleset has to price set scores rather than draws, that a
/// football ruleset which does not record goals cannot produce a score, or
/// that a metric named here has to exist for that sport.
///
/// Kept apart from the endpoints because creating and editing ask the same
/// question, and two copies of this would answer it differently within a
/// release.
/// </summary>
internal sealed class RulesetPolicy(SportFrogDbContext database)
{
    /// <summary>
    /// Everything wrong with the configuration, or nothing.
    /// </summary>
    /// <remarks>
    /// All of it at once rather than the first problem: the caller is
    /// assembling a form, and being told one mistake per attempt turns that
    /// into five round trips.
    /// </remarks>
    public async Task<IReadOnlyList<RulesetViolation>> InspectAsync(
        string sportCode,
        RulesetConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var sport = await database.Sports
            .AsNoTracking()
            .Include(candidate => candidate.Metrics)
            .SingleOrDefaultAsync(candidate => candidate.Code == sportCode, cancellationToken);

        if (sport is null)
        {
            // Nothing else can be judged without knowing the sport, and
            // reporting the consequences of an unknown sport would bury the
            // one problem the caller actually has.
            return [new RulesetViolation(
                "SportCode",
                "That sport is not one the platform supports.")];
        }

        var violations = new List<RulesetViolation>();

        var periodsAreUsable = InspectPeriods(sport, configuration, violations);

        InspectMetrics(sport, configuration, violations);

        // Both of these are read against the set of scorelines the match can
        // finish on, and that set is derived from the number of periods. With
        // an unusable number there is nothing to derive: reporting what the
        // outcomes should have been would be answering a question the caller
        // has not asked yet, and burying the one they need to fix first.
        if (periodsAreUsable)
        {
            InspectPoints(sport, configuration, violations);
            InspectWalkover(sport, configuration, violations);
        }

        return violations;
    }

    /// <summary>
    /// A sport played in sets needs a deciding one.
    /// </summary>
    /// <returns>
    /// Whether the periods can be reasoned from, which the checks that derive
    /// the possible scorelines depend on.
    /// </returns>
    private static bool InspectPeriods(
        Sport sport,
        RulesetConfiguration configuration,
        List<RulesetViolation> violations)
    {
        if (sport.ScoreMode == ScoreMode.Sets && configuration.Periods.Count % 2 == 0)
        {
            violations.Add(new RulesetViolation(
                "Config.Periods.Count",
                $"{sport.Name} is played in sets, so the number of sets must be odd: " +
                "an even number leaves a match that cannot be won."));

            return false;
        }

        // A period measured in minutes where the period ends on a score
        // instead is not refused, only pointless: it describes a clock nobody
        // reads. Refusing it would block the league that does run a time
        // limit per set, for scheduling reasons.

        return true;
    }

    /// <summary>
    /// The priced outcomes must be exactly the ones this sport can produce.
    /// </summary>
    private static void InspectPoints(
        Sport sport,
        RulesetConfiguration configuration,
        List<RulesetViolation> violations)
    {
        var required = MatchOutcomes.RequiredFor(sport.ScoreMode, configuration.Periods.Count);

        var permitted = new HashSet<string>(required, StringComparer.Ordinal);
        permitted.UnionWith(MatchOutcomes.OptionalFor(sport.ScoreMode));

        var missing = required
            .Where(outcome => !configuration.Points.ContainsKey(outcome))
            .ToList();

        if (missing.Count > 0)
        {
            violations.Add(new RulesetViolation(
                "Config.Points",
                $"These outcomes have no value: {string.Join(", ", missing)}. " +
                $"A {sport.Name} match can end in any of them, and a table cannot be " +
                "built from a result that is worth nothing in particular."));
        }

        var unknown = configuration.Points.Keys
            .Where(outcome => !permitted.Contains(outcome))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (unknown.Count > 0)
        {
            violations.Add(new RulesetViolation(
                "Config.Points",
                $"These outcomes cannot happen in {sport.Name}: {string.Join(", ", unknown)}. " +
                $"It can end in: {string.Join(", ", permitted.Order(StringComparer.Ordinal))}."));
        }
    }

    /// <summary>
    /// Metrics must belong to the sport, and must be enough to produce a
    /// score.
    /// </summary>
    private static void InspectMetrics(
        Sport sport,
        RulesetConfiguration configuration,
        List<RulesetViolation> violations)
    {
        if (configuration.Metrics is not { } chosen)
        {
            // Absent means every metric the sport defines, which is by
            // definition a valid selection.
            return;
        }

        var available = sport.Metrics
            .Select(metric => metric.Code)
            .ToHashSet(StringComparer.Ordinal);

        var unknown = chosen
            .Where(code => !available.Contains(code))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (unknown.Count > 0)
        {
            violations.Add(new RulesetViolation(
                "Config.Metrics",
                $"{sport.Name} has no such events: {string.Join(", ", unknown)}. " +
                $"Available: {string.Join(", ", available.Order(StringComparer.Ordinal))}."));
        }

        // Under a cumulative score the result is the sum of the scoring
        // events, so leaving one out does not merely stop it being counted as
        // a statistic: it makes the score unreachable. Under sets the score
        // comes from the periods won and no metric carries it, so there is
        // nothing here to require.
        var scoring = sport.Metrics
            .Where(metric => metric.AffectsScore)
            .Select(metric => metric.Code)
            .Where(code => !chosen.Contains(code, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (scoring.Count > 0)
        {
            violations.Add(new RulesetViolation(
                "Config.Metrics",
                $"These events decide the score in {sport.Name} and cannot be left out: " +
                string.Join(", ", scoring) + "."));
        }
    }

    /// <summary>
    /// A walkover has to be recordable as a real result of this sport.
    /// </summary>
    private static void InspectWalkover(
        Sport sport,
        RulesetConfiguration configuration,
        List<RulesetViolation> violations)
    {
        if (configuration.Walkover is not { } walkover || sport.ScoreMode != ScoreMode.Sets)
        {
            return;
        }

        // Where the match score is sets won, the score awarded for a walkover
        // has to be a scoreline the match could have finished on.
        var toWin = (configuration.Periods.Count + 1) / 2;

        if (walkover.WinnerScore != toWin)
        {
            violations.Add(new RulesetViolation(
                "Config.Walkover.WinnerScore",
                $"A {sport.Name} match is won at {toWin} sets, so a walkover is recorded " +
                $"with {toWin} and not {walkover.WinnerScore}."));
        }

        if (walkover.LoserScore >= toWin)
        {
            violations.Add(new RulesetViolation(
                "Config.Walkover.LoserScore",
                $"The side that did not appear cannot be credited with {toWin} sets or more."));
        }
    }
}

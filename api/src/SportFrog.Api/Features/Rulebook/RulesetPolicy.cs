using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Rulebook;

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
///
/// What is mode-specific — the period-count constraint, the walkover
/// scoreline — is delegated to <see cref="IRulesetShapeRules"/>, resolved
/// through <see cref="IRulesetShapeRulesRegistry"/> rather than a mode
/// check, so a mode this class does not already know is a new registration
/// and not a new branch. Pricing itself is asked of
/// <see cref="IMatchOutcomeRulesRegistry"/> the same way.
/// </summary>
internal sealed class RulesetPolicy(
    SportFrogDbContext database,
    IMatchOutcomeRulesRegistry outcomeRules,
    IRulesetShapeRulesRegistry shapeRules)
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
                "Ese deporte no es uno de los que soporta la plataforma.")];
        }

        var violations = new List<RulesetViolation>();
        var shape = shapeRules.For(sport.ScoreMode);

        var periodsAreUsable = shape.InspectPeriods(sport, configuration, violations);

        InspectMetrics(sport, configuration, violations);

        // Both of these are read against the set of scorelines the match can
        // finish on, and that set is derived from the number of periods. With
        // an unusable number there is nothing to derive: reporting what the
        // outcomes should have been would be answering a question the caller
        // has not asked yet, and burying the one they need to fix first.
        if (periodsAreUsable)
        {
            InspectPoints(sport, configuration, violations);
            shape.InspectWalkover(sport, configuration, violations);
        }

        return violations;
    }

    /// <summary>
    /// The priced outcomes must be exactly the ones this sport can produce.
    /// </summary>
    private void InspectPoints(
        Sport sport,
        RulesetConfiguration configuration,
        List<RulesetViolation> violations)
    {
        var rules = outcomeRules.For(sport.ScoreMode);
        var required = rules.RequiredOutcomes(configuration.Periods.Count);

        var permitted = new HashSet<string>(required, StringComparer.Ordinal);
        permitted.UnionWith(rules.OptionalOutcomes());

        var missing = required
            .Where(outcome => !configuration.Points.ContainsKey(outcome))
            .ToList();

        if (missing.Count > 0)
        {
            violations.Add(new RulesetViolation(
                "Config.Points",
                $"Estos desenlaces no tienen valor: {string.Join(", ", missing)}. " +
                $"Un partido de {sport.Name} puede terminar en cualquiera de ellos, y una " +
                "tabla no se puede construir con un resultado que no vale nada en particular."));
        }

        var unknown = configuration.Points.Keys
            .Where(outcome => !permitted.Contains(outcome))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (unknown.Count > 0)
        {
            violations.Add(new RulesetViolation(
                "Config.Points",
                $"Estos desenlaces no pueden pasar en {sport.Name}: {string.Join(", ", unknown)}. " +
                $"Puede terminar en: {string.Join(", ", permitted.Order(StringComparer.Ordinal))}."));
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
                $"{sport.Name} no tiene esos eventos: {string.Join(", ", unknown)}. " +
                $"Disponibles: {string.Join(", ", available.Order(StringComparer.Ordinal))}."));
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
                $"Estos eventos deciden el marcador en {sport.Name} y no se pueden dejar afuera: " +
                string.Join(", ", scoring) + "."));
        }
    }
}

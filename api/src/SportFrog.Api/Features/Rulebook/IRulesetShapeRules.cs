using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Whether a ruleset's own shape — its period count, its walkover score —
/// makes sense for the score mode it claims to be for.
/// </summary>
/// <remarks>
/// A third question, distinct from the two <c>Matches</c> already asks: this
/// judges a <em>ruleset</em>, not a reported result (<c>IResultShapeRules</c>)
/// and not how a result is priced (<c>IMatchOutcomeRules</c>). A sport whose
/// ruleset needs no shape constraint at all — cumulative, today — still
/// implements this; it simply has nothing to add.
/// </remarks>
internal interface IRulesetShapeRules
{
    /// <summary>
    /// The score mode this is the rules for — the key
    /// <see cref="IRulesetShapeRulesRegistry"/> resolves it by.
    /// </summary>
    ScoreMode Mode { get; }

    /// <summary>
    /// A sport played in sets needs a deciding one.
    /// </summary>
    /// <returns>
    /// Whether the periods can be reasoned from, which the checks that derive
    /// the possible scorelines depend on.
    /// </returns>
    bool InspectPeriods(Sport sport, RulesetConfiguration configuration, List<RulesetViolation> violations);

    /// <summary>
    /// A walkover has to be recordable as a real result of this sport.
    /// </summary>
    void InspectWalkover(Sport sport, RulesetConfiguration configuration, List<RulesetViolation> violations);
}

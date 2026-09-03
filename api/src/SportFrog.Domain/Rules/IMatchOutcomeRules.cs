using SportFrog.Domain.Matches;

namespace SportFrog.Domain.Rules;

/// <summary>
/// How one score mode turns period scores into a match result: the
/// consolidated score, and the outcomes a ruleset must — and may — price.
/// </summary>
/// <remarks>
/// The seam <see cref="ScoreConsolidation"/> and <see cref="MatchOutcomes"/>
/// used to branch on internally, made into something a new mode can
/// implement instead of a case those two classes would otherwise need
/// editing to add. Deliberately narrow: this is only the arithmetic of
/// deciding and pricing a result. Whether a reported score could actually
/// have happened is a different question, answered where <c>ResultPolicy</c>
/// answers it — mixing the two into one interface would force every mode to
/// implement validation it may not need just to get the scoring it does.
///
/// A sport does not implement this directly. It declares a
/// <see cref="ScoreMode"/> and gets the matching rules, because scoring is a
/// property of the mode a competition is played in and not of the sport
/// itself — football and basketball are both cumulative, badminton and
/// volleyball are both sets. A sport whose scoring genuinely does not fit
/// either gets a new implementation of this interface, not a subclass of one
/// of the two that are here: <see cref="CumulativeMatchOutcomeRules"/> and
/// <see cref="SetsMatchOutcomeRules"/> are siblings, not a base and a
/// variant, because a caller must never be able to assume one behaves like
/// the other just because it was born from it.
/// </remarks>
public interface IMatchOutcomeRules
{
    /// <summary>
    /// The score mode this is the rules for — the key
    /// <see cref="IMatchOutcomeRulesRegistry"/> resolves it by.
    /// </summary>
    ScoreMode Mode { get; }

    /// <summary>The match score, read the way this mode reads it.</summary>
    (int Home, int Away) Consolidate(IReadOnlyList<PeriodScore> periods);

    /// <summary>Outcomes a ruleset for this mode must price.</summary>
    IReadOnlyCollection<string> RequiredOutcomes(short periods);

    /// <summary>Outcomes it may price, but need not.</summary>
    IReadOnlyCollection<string> OptionalOutcomes();

    /// <summary>Which outcome a finished match was, seen from one side.</summary>
    string OutcomeFor(int own, int against);
}

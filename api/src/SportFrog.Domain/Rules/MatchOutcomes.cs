namespace SportFrog.Domain.Rules;

/// <summary>
/// The ways a match of a given sport can end, which is the set of outcomes a
/// ruleset has to put a price on.
/// </summary>
/// <remarks>
/// Under <see cref="ScoreMode.Cumulative"/> a match is won, drawn or lost and
/// there is nothing to derive. Under <see cref="ScoreMode.Sets"/> the
/// outcomes depend on how many periods are played: best of five ends 3-0,
/// 3-1 or 3-2, best of three ends 2-0 or 2-1, and a league that awards a
/// point for taking a set off the winner needs those spelled out separately.
///
/// The derivation itself now lives in <see cref="IMatchOutcomeRules"/> and
/// its implementations, resolved through <see cref="IMatchOutcomeRulesRegistry"/>
/// — this stays as the entry point every existing caller already uses, so
/// nothing that calls it today has to change to keep working. Its own
/// registry is a separate, hardcoded instance rather than the container's:
/// this class is reached from <c>SportFrog.Domain</c>, which has no DI
/// container to resolve one from, so a new mode has to be added here by hand
/// — this is the one place in the mode-strategy design that is not wired
/// through <c>Program.cs</c>, and it is exactly the seam a new mode can be
/// forgotten at.
/// </remarks>
public static class MatchOutcomes
{
    public const string Win = "win";
    public const string Draw = "draw";
    public const string Loss = "loss";

    private static readonly IMatchOutcomeRulesRegistry Registry =
        new MatchOutcomeRulesRegistry(
            [new CumulativeMatchOutcomeRules(), new SetsMatchOutcomeRules(), new JudgedMatchOutcomeRules()]);

    /// <summary>
    /// Outcomes a ruleset for this sport must price.
    /// </summary>
    public static IReadOnlyCollection<string> RequiredFor(ScoreMode mode, short periods) =>
        Registry.For(mode).RequiredOutcomes(periods);

    /// <summary>
    /// Which outcome a finished match was, seen from one side.
    /// </summary>
    /// <remarks>
    /// The other half of the same idea, and it belongs here rather than in
    /// the standings for one reason: this has to produce exactly the keys
    /// <see cref="RequiredFor"/> asked the ruleset to price. Written twice
    /// they would drift by a character, and a table would silently award zero
    /// for a result nobody had failed to price — the ruleset would look
    /// complete and the points would be wrong.
    /// </remarks>
    public static string For(ScoreMode mode, int own, int against) =>
        Registry.For(mode).OutcomeFor(own, against);

    /// <summary>
    /// Outcomes it may price, but need not.
    /// </summary>
    public static IReadOnlyCollection<string> OptionalFor(ScoreMode mode) =>
        Registry.For(mode).OptionalOutcomes();
}

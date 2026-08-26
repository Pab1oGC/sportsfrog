namespace SportFrog.Domain.Matches;

/// <summary>
/// Counts periods using the name the ruleset gives them.
/// </summary>
/// <remarks>
/// The label is business data written by the organizers — tiempo, cuarto,
/// set — while the messages around it are in English. Dropping a number in
/// front of it produces "2 tiempo", which reads like a bug even though
/// nothing is wrong.
///
/// Pluralized by adding an s, which is correct for every label the catalog
/// seeds and for the ordinary Spanish period noun: tiempos, cuartos, sets,
/// mangas. It is a rule about the shape of these particular words, not a
/// general theory of Spanish plurals — a label ending in a consonant other
/// than s would want "es" and would come out wrong here.
///
/// If an organization ever needs a label this cannot pluralize, the fix is to
/// let the ruleset carry both forms rather than to make this cleverer:
/// guessing harder would still be guessing, and the organizers already know
/// the answer.
/// </remarks>
public static class PeriodLabel
{
    /// <summary>
    /// A count and its label, agreeing in number: "1 tiempo", "2 tiempos".
    /// </summary>
    public static string Count(int howMany, string label) =>
        $"{howMany} {(howMany == 1 ? label : Plural(label))}";

    /// <summary>
    /// The label on its own, plural: "sets".
    /// </summary>
    public static string Plural(string label) =>
        label.EndsWith('s') ? label : label + "s";
}

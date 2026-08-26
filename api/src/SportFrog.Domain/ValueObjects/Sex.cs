namespace SportFrog.Domain.ValueObjects;

/// <summary>
/// The sex recorded on an athlete, and the one a category can be restricted
/// to.
/// </summary>
/// <remarks>
/// Shared because the two are compared against each other. A roster is
/// accepted when the athlete's value matches what the category admits, and
/// that comparison is only meaningful if both sides were written from the
/// same short list. An athlete recorded as "Femenino" and a category admitting
/// "F" describe the same person and would never match — and the failure would
/// surface much later, as an eligible player being refused, rather than at the
/// point where the nonsense was written.
///
/// Closed for that reason and no other. It is a registration field used to
/// decide who may play in which division, not a statement about identity, and
/// it is deliberately narrow: sports categories are drawn along this line, so
/// this is the line the eligibility check needs.
///
/// Absent is a third state rather than a third value — an athlete whose sex
/// was not recorded, a category open to anyone — so it stays null instead of
/// being given a label.
///
/// Stored uppercase, which is the form the interface and the sample data
/// already use.
/// </remarks>
public static class Sex
{
    public const string Female = "F";
    public const string Male = "M";

    /// <summary>
    /// The form the value is stored and compared in. Blank and absent are the
    /// same statement, and both become absent.
    /// </summary>
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    /// <summary>
    /// Whether a value that was given is one of the two. An absent value is
    /// not checked here: whether it is allowed to be absent is a question for
    /// the field, not for the value.
    /// </summary>
    public static bool IsAcceptable(string? value) =>
        Normalize(value) is Female or Male;

    public static string Requirement =>
        $"Must be {Female} or {Male}, or left unset.";
}

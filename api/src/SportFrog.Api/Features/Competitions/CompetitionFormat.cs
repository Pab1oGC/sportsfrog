namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// How a competition draws its fixtures.
/// </summary>
/// <remarks>
/// The schema leaves this column as free text with no constraint. It is
/// closed here anyway, because the module that generates fixtures will have
/// to branch on it: a value it does not recognize is a competition whose
/// calendar cannot be drawn, and finding that out at scheduling time is
/// finding it out far too late.
///
/// Closing it in the application rather than in the schema is deliberate.
/// Adding a format means writing the draw for it, which is application work;
/// a CHECK constraint would put half of that decision in a migration and
/// invite the two halves to disagree.
/// </remarks>
internal static class CompetitionFormat
{
    /// <summary>Everyone plays everyone. One table, ordered by the ruleset.</summary>
    public const string League = "league";

    /// <summary>Single elimination. Losing ends the run.</summary>
    public const string Knockout = "knockout";

    /// <summary>Group stage first, then a knockout draw among those who advance.</summary>
    public const string Groups = "groups";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        League,
        Knockout,
        Groups,
    };
}

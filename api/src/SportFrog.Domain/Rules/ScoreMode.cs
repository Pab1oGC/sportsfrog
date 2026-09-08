namespace SportFrog.Domain.Rules;

/// <summary>
/// How a sport arrives at the result of a match.
/// </summary>
/// <remarks>
/// The distinction is not cosmetic: it decides where the score comes from.
/// Under <see cref="Cumulative"/> the result is the sum of the scoring events
/// recorded during the match, so a goal is both a statistic and a point.
/// Under <see cref="Sets"/> it is the count of periods won, consolidated from
/// the per-period scores — a volleyball point moves the set, and only the set
/// moves the match. Under <see cref="Judged"/> it is a score judges hand down
/// directly for a single performance — nothing is summed and nothing is won
/// period by period, and a tie is refused rather than recorded.
///
/// Stored as text rather than as a database enum, unlike membership_role: the
/// column carries a CHECK constraint in the schema and the catalog is seeded
/// by migration, so there is no gain in a type that would have to be altered
/// to admit a new mode — <see cref="Judged"/> itself is the proof, added
/// without touching the constraint's shape.
/// </remarks>
public enum ScoreMode
{
    Cumulative,
    Sets,

    /// <summary>
    /// Decided by a score judges hand down for one performance a side, not by
    /// events summed or periods won. Poomsae's bracket bouts are the first
    /// sport to use it.
    /// </summary>
    Judged,
}

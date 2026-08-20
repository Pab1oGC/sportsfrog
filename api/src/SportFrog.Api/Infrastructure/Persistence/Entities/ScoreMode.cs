namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// How a sport arrives at the result of a match.
/// </summary>
/// <remarks>
/// The distinction is not cosmetic: it decides where the score comes from.
/// Under <see cref="Cumulative"/> the result is the sum of the scoring events
/// recorded during the match, so a goal is both a statistic and a point.
/// Under <see cref="Sets"/> it is the count of periods won, consolidated from
/// the per-period scores — a volleyball point moves the set, and only the set
/// moves the match.
///
/// Stored as text rather than as a database enum, unlike membership_role: the
/// column carries a CHECK constraint in the schema and the catalog is seeded
/// by migration, so there is no gain in a type that would have to be altered
/// to admit a new mode.
/// </remarks>
public enum ScoreMode
{
    Cumulative,
    Sets,
}

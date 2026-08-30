namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A club taking part in one category of a competition.
///
/// Not a synonym for a club. A club that enters three categories of the same
/// league produces three teams, each with its own roster, its own fixtures
/// and its own place in a table. "Deportivo Central" is a club; "Deportivo
/// Central" in Sub-15 masculino is a team.
/// </summary>
/// <remarks>
/// Deleting is logical, and the reason is what hangs off it. Matches
/// reference teams with ON DELETE RESTRICT, so a team that ever played is
/// named by a result; hiding it has to leave that result intact and readable.
/// </remarks>
public sealed class Team
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid ClubId { get; set; }

    public Guid CategoryId { get; set; }

    /// <summary>
    /// What this team is called in fixtures and standings.
    /// </summary>
    /// <remarks>
    /// Its own field rather than the club's name, because a club with two
    /// teams in one competition needs to tell them apart — "Central A" and
    /// "Central B" — and because the name a team plays under is not always
    /// the club's registered name.
    /// </remarks>
    public required string Name { get; set; }

    /// <summary>
    /// Which group it was drawn into, where the format has groups.
    /// </summary>
    /// <remarks>
    /// Null in a league or a straight knockout. Free text because it is a
    /// label on a draw — "A", "Zona Norte" — and nothing computes from it
    /// beyond grouping the table.
    /// </remarks>
    public string? GroupLabel { get; set; }

    /// <summary>
    /// Which pot this team sits in for a seeded group draw.
    /// </summary>
    /// <remarks>
    /// Null means the team draws from no particular pot — which is also what
    /// every team having no pot at all means, and is exactly a plain random
    /// draw. Set by hand ahead of a draw that wants to keep, say, the
    /// strongest side of each pot apart; read by the draw and never written
    /// by anything else.
    /// </remarks>
    public short? Seed { get; set; }

    /// <summary>
    /// Whether the team is still competing.
    /// </summary>
    /// <remarks>
    /// A team that withdraws mid-season stops being scheduled but keeps the
    /// matches it already played, and keeps appearing in the table. That is a
    /// different fact from the enrollment having been a mistake, which is
    /// what deletion records.
    /// </remarks>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Club? Club { get; set; }
    public Category? Category { get; set; }

    /// <summary>
    /// Everyone ever registered for this team, including those who withdrew.
    /// Who is currently available is a filter over this, not a separate list.
    /// </summary>
    public ICollection<RosterEntry> Roster { get; set; } = [];
}

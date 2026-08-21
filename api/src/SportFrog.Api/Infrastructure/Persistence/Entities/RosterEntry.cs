namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A person registered to play for a team.
///
/// This is what makes an athlete eligible: events are recorded against a
/// roster entry, not against a person, so the same athlete playing for two
/// teams across two seasons produces two entries and two separate sets of
/// statistics.
/// </summary>
/// <remarks>
/// Three ways of leaving, and the schema keeps them apart because they are
/// different facts.
///
/// <see cref="WithdrawnAt"/> is a sporting withdrawal: the player left the
/// team mid-competition. Everything they did while they were on it stays on
/// record, and their shirt number goes back into circulation.
///
/// <see cref="DeletedAt"/> is an administrative correction: the registration
/// should never have been made. It hides the entry, and is refused once
/// anything has been recorded against it — at that point the honest answer is
/// a withdrawal, not a pretence that it never happened.
///
/// Neither is the athlete leaving the organization, which is the athlete's
/// own <c>is_active</c> and says nothing about a particular team.
/// </remarks>
public sealed class RosterEntry
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid TeamId { get; set; }

    public Guid AthleteId { get; set; }

    /// <summary>
    /// The shirt they play in. Unique within the team while they are on it.
    /// </summary>
    /// <remarks>
    /// Optional, because a squad list is often drawn up before the numbers
    /// are handed out, and a competition recording only scores never needs
    /// them at all.
    ///
    /// The uniqueness ignores withdrawn players: a number freed when someone
    /// leaves belongs to whoever takes their place, which is what happens on
    /// the pitch.
    /// </remarks>
    public short? JerseyNumber { get; set; }

    /// <summary>
    /// Where they play: arquero, base, líbero.
    /// </summary>
    /// <remarks>
    /// Free text, and deliberately not a catalog. The positions of five
    /// sports do not form one list, they change by league and by decade, and
    /// nothing computes from this — it is printed on a team sheet and read by
    /// a person.
    /// </remarks>
    public string? Position { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    /// <summary>When they stopped playing for this team, if they did.</summary>
    public DateTimeOffset? WithdrawnAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Team? Team { get; set; }
    public Athlete? Athlete { get; set; }
}

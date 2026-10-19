using System.Text.Json.Serialization;

namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// What one person does for a team without playing for it.
/// </summary>
/// <remarks>
/// A closed set, unlike <see cref="RosterEntry.Position"/>, which is free
/// text for a reason that does not hold here: a position is read off a team
/// sheet by a person and nothing computes from it, while this decides whether
/// a team already has a head coach — the database refuses a second one — and
/// which of the two groups an organizer asks for, cuerpo técnico or
/// dirigentes, a person belongs to.
///
/// Mapped to the PostgreSQL <c>staff_role</c> enum. The member names
/// translate to their snake_case counterparts in the database.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<StaffRole>))]
public enum StaffRole
{
    /// <summary>Director técnico. At most one per team at a time.</summary>
    HeadCoach,

    /// <summary>Asistente técnico.</summary>
    AssistantCoach,

    /// <summary>Preparador físico.</summary>
    PhysicalTrainer,

    /// <summary>Médico.</summary>
    Physician,

    /// <summary>Kinesiólogo.</summary>
    Physiotherapist,

    /// <summary>Delegado: who the organizer deals with on match day.</summary>
    Delegate,

    /// <summary>Dirigente, of the club rather than of the bench.</summary>
    Director,
}

/// <summary>
/// A person on a team's technical staff or its board — a coach, a doctor, a
/// delegate — registered so they can be accredited and printed a credential,
/// without being eligible to play.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="RosterEntry"/> carrying a flag. That table
/// documents itself as what makes an athlete eligible, and
/// <c>Features.Rosters.RosterPolicy</c> holds every one of its rows to the
/// category's age, weight and squad limits: a forty-five-year-old coach of a
/// Sub-15 team fails all of them and would take one of the squad's places.
/// Keeping the two apart is also what makes it impossible for a
/// <see cref="PlayerEvent"/> to name a doctor — its foreign key has nowhere
/// to point — instead of leaving every reader of a roster to ask whether each
/// row is a player at all.
///
/// The person is an <see cref="Athlete"/> row, which is already the
/// organization's table of people: identified by document rather than by name,
/// so a later registration attaches to the same person. Nothing about a coach
/// needs a second table of people, and sharing this one is what lets them be
/// accredited (<see cref="AthleteAccreditation"/>) and printed on a credential
/// with no change to either — a credential carries no shirt number or
/// position, so a coach fits the card exactly as a player does.
///
/// Leaving mirrors a registration's own two ways out, for the same reasons:
/// <see cref="WithdrawnAt"/> is the staff member who left mid-season and whose
/// time on the team stays on record, <see cref="DeletedAt"/> the row that
/// should never have been written.
/// </remarks>
public sealed class TeamStaff
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid TeamId { get; set; }

    /// <summary>The person, from the organization's own people.</summary>
    public Guid AthleteId { get; set; }

    public StaffRole Role { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    /// <summary>When they stopped working for this team, if they did.</summary>
    public DateTimeOffset? WithdrawnAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Team? Team { get; set; }
    public Athlete? Athlete { get; set; }
}

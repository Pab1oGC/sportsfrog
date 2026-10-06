namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Which accreditation category a person holds in one competition.
/// </summary>
/// <remarks>
/// Deliberately not a column on <see cref="RosterEntry"/>, though that is
/// where the shirt number and the position live. A roster entry is about a
/// person in a team, and somebody registered with two teams of the same
/// competition has two of them — which, as a place to keep the accreditation,
/// would mean two cards for one person. It hangs off the competition instead,
/// where there is exactly one of it.
/// </remarks>
public sealed class AthleteAccreditation
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid CompetitionId { get; set; }

    public Guid AthleteId { get; set; }

    public Guid CategoryId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Athlete? Athlete { get; set; }

    public AccreditationCategory? Category { get; set; }

    /// <summary>What this person has beyond, or short of, their category.</summary>
    public ICollection<AthleteAccreditationItem> Overrides { get; set; } = [];
}

/// <summary>
/// One exception to what a category carries, for one person.
/// </summary>
/// <remarks>
/// Absent is the ordinary case: most people get exactly their package, and the
/// rows that exist here are the handful somebody decided about one at a time.
/// </remarks>
public sealed class AthleteAccreditationItem
{
    public Guid OrgId { get; set; }

    public Guid CompetitionId { get; set; }

    public Guid AccreditationId { get; set; }

    public Guid ItemId { get; set; }

    /// <summary>
    /// True adds something the category does not carry; false takes away
    /// something it does.
    /// </summary>
    /// <remarks>
    /// Both directions, because both happen. Somebody may need a door their
    /// category does not open, and somebody serving a sanction loses one it
    /// does — and recording the second as "granted = false" rather than by
    /// moving them to another category keeps the reason where it belongs, on
    /// the person, instead of inventing a category of one.
    /// </remarks>
    public bool Granted { get; set; }

    public AthleteAccreditation? Accreditation { get; set; }

    public AccreditationItem? Item { get; set; }
}

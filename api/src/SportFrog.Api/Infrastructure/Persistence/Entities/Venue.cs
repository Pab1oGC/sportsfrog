namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A place where matches are played: a club's ground, a school, a municipal
/// sports centre.
///
/// A venue is an address, not a thing anything is booked on. What gets booked
/// is a <see cref="VenueSpace"/> — the pitch, the court, field number two —
/// and a venue with only one of them still has one, so nothing has to
/// distinguish the simple case from the composite one.
/// </summary>
/// <remarks>
/// The table carries no deleted_at, so removal is physical, and that is only
/// possible while nothing was ever played here. What retires a venue that has
/// history is <see cref="IsActive"/>: it stops being offered for new
/// fixtures and the ones already played keep pointing at it.
/// </remarks>
public sealed class Venue
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>
    /// Unique within the organization, and unlike a club's name this
    /// uniqueness is total: there are no hidden rows for a freed name to
    /// collide with, because a removed venue is gone.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Where it is, as a person would tell someone driving there. Free text
    /// because nothing computes from it, and optional because a league whose
    /// venues everyone already knows should not have to invent addresses.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>Whether it is still offered when a fixture is being placed.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<VenueSpace> Spaces { get; set; } = [];
}

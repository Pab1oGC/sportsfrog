namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Something at a venue that one match at a time can be played on: a pitch, a
/// court, field two.
///
/// This is the bookable thing. A fixture is placed on a space at a time, and
/// the schema will not let two live fixtures share the pair — so a space is
/// the unit the calendar reasons about, and a venue with three pitches can
/// run three matches at once while a venue with one cannot.
/// </summary>
/// <remarks>
/// Removal is physical, like its venue, and for the same reason: there is no
/// deleted_at to hide behind. A space that ever hosted a fixture is retired
/// with <see cref="IsActive"/> instead.
///
/// It is worth being clear about what the flags mean together. A space is
/// offered for a new fixture only when both it and its venue are active: a
/// venue closed for refurbishment closes its pitches with it, without anything
/// having to walk down and mark each one.
/// </remarks>
public sealed class VenueSpace
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid VenueId { get; set; }

    /// <summary>Unique within its venue. "Cancha 1", "Court B", "Principal".</summary>
    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Venue? Venue { get; set; }
}

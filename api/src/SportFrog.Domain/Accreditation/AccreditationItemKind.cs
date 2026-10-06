using System.Text.Json.Serialization;

namespace SportFrog.Domain.Accreditation;

/// <summary>
/// What a catalogue entry is, which is the same as asking where on the card it
/// is printed.
/// </summary>
/// <remarks>
/// One enum rather than four tables, because a discipline, a venue, a service
/// and a zone are one shape: a short code, a name, and a line in the glossary
/// on the back of every card. The only thing that tells them apart is the
/// block that draws them, and that is exactly what this says.
///
/// The order is the printing order, and it is not decoration: the front's
/// first row opens with the discipline and continues with the venues, the
/// second row carries the services, and the footer band carries the zones.
/// Reordering these members reorders the card.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<AccreditationItemKind>))]
public enum AccreditationItemKind
{
    /// <summary>The sport the credential is for — <c>FUT</c>. One per card.</summary>
    Discipline,

    /// <summary>A place the holder may enter — the village, the press centre.</summary>
    Venue,

    /// <summary>Something the holder is entitled to — transport, dining.</summary>
    Service,

    /// <summary>
    /// An access zone, printed large along the foot of both faces.
    /// </summary>
    /// <remarks>
    /// The one kind read at arm's length: a steward at a door checks the band
    /// and nothing else, which is why it is the only kind that gets a block of
    /// the card to itself rather than a box in a row.
    /// </remarks>
    Zone,
}

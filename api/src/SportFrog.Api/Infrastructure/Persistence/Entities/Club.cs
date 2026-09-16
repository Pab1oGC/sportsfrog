namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A club taking part in an organization's competitions (RF-07).
///
/// A club is not a team: it enters a category through a team, and the same
/// club fields one per category it competes in.
///
/// Deleting is always logical. Teams reference clubs with ON DELETE RESTRICT,
/// so a club that ever competed has history hanging off it.
/// </summary>
public sealed class Club
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public required string Name { get; set; }

    /// <summary>Abbreviation for standings tables and fixtures, where space is short.</summary>
    public string? ShortName { get; set; }

    public string? LogoUrl { get; set; }

    /// <summary>
    /// Where to tell this club something about one of its own fixtures
    /// changed — a match rescheduled to another ground or instant, today the
    /// only thing that writes to it. Null means nobody is told.
    /// </summary>
    public string? ContactEmail { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The one club, per organization, that an athlete with no delegation of
    /// their own enrolls under. Not a real club, so it is excluded from
    /// listings and pickers meant for clubs a delegate manages.
    /// </summary>
    public bool IsUnaffiliated { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

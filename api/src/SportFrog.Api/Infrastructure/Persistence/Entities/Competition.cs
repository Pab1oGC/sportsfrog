namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A tournament or league an organization runs (RF-06).
///
/// It binds together everything the rest of the system hangs off: one sport,
/// one ruleset, and the categories that actually field teams. A match belongs
/// to a category and a category belongs to a competition, so this row is what
/// separates one season from the next.
/// </summary>
/// <remarks>
/// Deleting is logical. Issued documents reference a competition with
/// ON DELETE RESTRICT, and its results are read long after it ends: a
/// credential printed for a player names the competition it was issued for,
/// and that name has to keep resolving.
/// </remarks>
public sealed class Competition
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>
    /// Repeated from the ruleset rather than read through it, because the
    /// schema puts a foreign key on it: categories, teams and matches are
    /// reached from here and the sport has to be answerable without a join
    /// through rules that may be overridden further down.
    /// </summary>
    public required string SportCode { get; set; }

    /// <summary>The rules the competition is played and ranked by, unless a category overrides them.</summary>
    public Guid RulesetId { get; set; }

    public required string Name { get; set; }

    /// <summary>Second segment of the public address, after the organization's.</summary>
    public required string Slug { get; set; }

    /// <summary>What the organizers call this edition: "2026", "Apertura 2026", "Verano".</summary>
    public required string Season { get; set; }

    /// <summary>How the fixtures are drawn. See <see cref="Features.Competitions.CompetitionFormat"/>.</summary>
    public required string Format { get; set; }

    public CaptureLevel CaptureLevel { get; set; } = CaptureLevel.Basic;

    public CompetitionState Status { get; set; } = CompetitionState.Draft;

    public DateOnly? StartsOn { get; set; }

    public DateOnly? EndsOn { get; set; }

    /// <summary>
    /// Whether the competition resolves to a public page at all.
    /// </summary>
    /// <remarks>
    /// Read by <c>resolve_public_competition</c>, which the read-only
    /// database user calls before any isolation context exists. A competition
    /// that is not public does not resolve, and the caller cannot tell it
    /// apart from one that does not exist.
    /// </remarks>
    public bool IsPublic { get; set; }

    public required CompetitionSettings Settings { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Sport? Sport { get; set; }
    public Ruleset? Ruleset { get; set; }
}

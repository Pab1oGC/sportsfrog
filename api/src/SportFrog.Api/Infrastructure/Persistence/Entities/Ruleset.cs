namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// An organization's rules for playing a sport.
///
/// The sport says football is played in halves and scored in goals; the
/// ruleset says this league plays 45-minute halves, awards three points a
/// win, and breaks ties by goal difference. One organization can hold several
/// for the same sport — a senior league and a youth one rarely play by the
/// same rules — and a competition points at exactly one.
///
/// Belongs to an organization, so it carries org_id and is subject to the
/// isolation policies.
/// </summary>
/// <remarks>
/// Deletion is physical, unlike clubs and athletes: the table has no
/// deleted_at because a ruleset is a setting rather than a record of
/// something that happened. What protects the history is the reference —
/// competitions and categories point at it, so one that was ever used cannot
/// be removed.
/// </remarks>
public sealed class Ruleset
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public required string SportCode { get; set; }

    /// <summary>How the people running the competitions refer to it. Unique within the organization.</summary>
    public required string Name { get; set; }

    public required RulesetConfiguration Config { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Sport? Sport { get; set; }
}

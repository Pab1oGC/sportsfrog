namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Something one player did in one match: a goal, a card, an assist.
///
/// Recorded against a registration rather than against a person, which is
/// what makes the statistics answerable. The same athlete playing two seasons
/// for two clubs has two registrations, so "goals for Club Norte in the 2026
/// Apertura" is a filter rather than a reconstruction.
/// </summary>
/// <remarks>
/// Removal is physical: the table carries no deleted_at, and an event entered
/// by mistake is not a fact that happened and was undone — it is a fact that
/// never happened. A goal that was scored and later disallowed is corrected
/// by whoever rules on it, not by keeping a hidden row.
///
/// That is also why registrations cannot be struck once events point at them
/// (ON DELETE RESTRICT): these rows are the reason the roster module keeps a
/// withdrawal and a strike apart.
/// </remarks>
public sealed class PlayerEvent
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid MatchId { get; set; }

    /// <summary>The registration, which names both the person and the team they did it for.</summary>
    public Guid RosterEntryId { get; set; }

    /// <summary>
    /// What happened, from the sport's catalog.
    /// </summary>
    /// <remarks>
    /// The catalog is shared across organizations and carries no org_id, so
    /// nothing stops a football match from naming a basketball rebound. That
    /// the metric belongs to the sport being played is checked before the row
    /// is written — the schema says so itself, because expressing it as a
    /// foreign key would mean copying the sport onto this table.
    /// </remarks>
    public Guid MetricId { get; set; }

    /// <summary>Which period it happened in. Null when nobody wrote it down.</summary>
    public short? PeriodNumber { get; set; }

    /// <summary>The minute, where the sport is played against a clock.</summary>
    public short? Minute { get; set; }

    /// <summary>
    /// How many. One by default, and more when several of the same thing are
    /// entered together — a run of free throws is tallied rather than typed
    /// out one at a time.
    /// </summary>
    public int Quantity { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Match? Match { get; set; }
    public RosterEntry? RosterEntry { get; set; }
    public SportMetric? Metric { get; set; }
}

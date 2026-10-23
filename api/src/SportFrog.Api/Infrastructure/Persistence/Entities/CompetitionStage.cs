namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// The logistics of one leg of a competition's draw — which pitches are
/// free for it, and how much margin to leave between its matches.
/// </summary>
/// <remarks>
/// A competition drawn as groups runs through more than one stage — see
/// <see cref="Competitions.CompetitionStages"/>, which says which ones — and
/// the group stage of a tournament rarely plays on the same pitches as its
/// final: a venue booked for the weekend the groups are decided is not
/// necessarily free the weekend the bracket is.
///
/// Not yet what the scheduler reads. <c>ScheduleCalendar</c> still reads the
/// one <c>ScheduleSettings</c> a competition carries as a whole, and the
/// migration this entity shipped with backfilled one row per stage every
/// existing competition's own draw already implies, copying that same
/// schedule onto each — a faithful snapshot of "this is what scheduling
/// already looked like," not yet a second opinion anything consults. Moving
/// the scheduler onto these, stage by stage, is later work.
///
/// <see cref="CategoryStage"/> is this row's sibling: that one holds what
/// varies by category — the reglamento, the group's qualifiers — this one
/// holds what is the same for every category playing the same stage of the
/// same event, because a pitch is booked for the competition, not for one
/// of its divisions.
/// </remarks>
public sealed class CompetitionStage
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid CompetitionId { get; set; }

    public StageKind Kind { get; set; }

    /// <summary>
    /// Minutes left on a pitch after one match of this stage before the next
    /// may start. Null leaves the scheduler's own default.
    /// </summary>
    public short? BufferMinutes { get; set; }

    /// <summary>
    /// Which pitches this stage may place a match on. Null or empty means
    /// none are enabled yet — the scheduler reports that rather than placing
    /// nothing silently.
    /// </summary>
    public Guid[]? SpaceIds { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Competition? Competition { get; set; }
}

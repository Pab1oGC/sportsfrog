using System.Text.Json.Serialization;

namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>What has become of one dated class.</summary>
/// <remarks>
/// Three states, same shape as <see cref="SportFrog.Domain.Matches.MatchState"/>'s
/// three real ones: a session not yet run, one that was, and one struck
/// from the calendar. Nothing in this project moves a session to
/// <see cref="Held"/> on its own — that transition belongs to whoever first
/// records something against it, same as a match is never marked finished
/// by a button of its own, only by a result being recorded.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<CourseSessionState>))]
public enum CourseSessionState
{
    /// <summary>On the calendar, not yet run.</summary>
    Scheduled,

    /// <summary>Run. Something was recorded against it.</summary>
    Held,

    /// <summary>Struck from the calendar. It owes nothing and produces nothing.</summary>
    Cancelled,
}

/// <summary>
/// One date a <see cref="Course"/> meets on.
/// </summary>
/// <remarks>
/// What <c>SessionPlanner</c> (the domain project) turns a weekly schedule
/// into — a recurring rule and the date it lands on are different
/// questions, the same way a drawn fixture and the pitch it is later placed
/// on are. <see cref="StartsAt"/>/<see cref="EndsAt"/> are copied from the
/// schedule slot at the moment this row is generated, not read through a
/// join: a schedule edited afterward must not reach back and move a class
/// that already has a date.
///
/// No <c>DeletedAt</c>: a session is generated once, then either cancelled
/// (with a reason) or moved, never struck as an administrative mistake the
/// way a roster entry or an enrollment can be — materializing a date from a
/// standing schedule is not the kind of thing that is "entered by
/// mistake". If the schedule itself was wrong, it is corrected and
/// regenerated; <c>GenerateSessions</c> only ever fills in dates that do
/// not already have a row, so running it again after a fix never
/// duplicates anything.
/// </remarks>
public sealed class CourseSession
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid CourseId { get; set; }

    public DateOnly SessionDate { get; set; }

    public TimeOnly StartsAt { get; set; }

    public TimeOnly EndsAt { get; set; }

    public CourseSessionState Status { get; set; } = CourseSessionState.Scheduled;

    /// <summary>Why it was cancelled. Set exactly when <see cref="Status"/> is <see cref="CourseSessionState.Cancelled"/>.</summary>
    public string? CancellationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Course? Course { get; set; }
}

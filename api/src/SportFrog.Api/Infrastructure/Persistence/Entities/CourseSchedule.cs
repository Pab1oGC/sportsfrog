namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// One weekly slot a <see cref="Course"/> meets in.
/// </summary>
/// <remarks>
/// No <c>UpdatedAt</c> and no <c>DeletedAt</c>: a schedule change replaces
/// every row of a course at once — <c>Features.Courses.CourseContract</c>
/// takes the whole set on every write — rather than editing one row in
/// place, the same shape <c>accreditation_category_items</c> already uses
/// for a set that is always replaced wholesale.
///
/// Two rows sharing a <see cref="DayOfWeek"/> are accepted — a morning and
/// an evening group on the same day — see <see cref="WeeklySlot"/>'s own
/// remarks for why that is a decision for whoever edits the schedule, not
/// for this row's shape.
/// </remarks>
public sealed class CourseSchedule
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid CourseId { get; set; }

    /// <summary>
    /// .NET's own <see cref="DayOfWeek"/>: 0 Sunday .. 6 Saturday, stored
    /// as that same number. Also what JavaScript's <c>Date#getDay()</c>
    /// returns, so the wire value crosses the API with no translation on
    /// either side.
    /// </summary>
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartsAt { get; set; }

    public TimeOnly EndsAt { get; set; }

    public Course? Course { get; set; }
}

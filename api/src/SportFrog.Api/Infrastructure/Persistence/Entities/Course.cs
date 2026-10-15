namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A course an organization's sports school runs: one sport, one weekly
/// schedule, one monthly fee.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="Category"/>. A category hangs off a
/// competition and carries a <see cref="Ruleset"/> because what happens
/// inside it is scored and ranked; a course has neither, because teaching a
/// group of children on Tuesdays and Thursdays is not a contest. Running it
/// through Category would mean inventing a competition and a ruleset that
/// answer no real question, just to borrow a name and a roster shape.
///
/// Deleting is logical, the same shape as <see cref="Club"/>: by the time a
/// course has students, sessions and pension charges hanging off it, erasing
/// the row would erase their history with it.
/// </remarks>
public sealed class Course
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>
    /// What discipline this course teaches. A direct foreign key rather
    /// than one reached through a <see cref="Ruleset"/> — a course has no
    /// ruleset to read it through, unlike <see cref="Competition.SportCode"/>.
    /// </summary>
    public required string SportCode { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Where this course meets. Null while nothing has been settled yet —
    /// a course can be registered before a pitch is assigned.
    /// </summary>
    public Guid? VenueSpaceId { get; set; }

    /// <summary>
    /// Who currently teaches it. Informational: nothing downstream reads
    /// this to decide who may take attendance or record a payment, so
    /// losing it to <c>ON DELETE SET NULL</c> the day a membership is
    /// removed costs a course its listed coach, never its history.
    /// </summary>
    public Guid? CoachMembershipId { get; set; }

    /// <summary>How many students this course admits. Null for no ceiling.</summary>
    public short? Capacity { get; set; }

    /// <summary>
    /// What one month costs, in bolivianos — see this entity's migration
    /// for why there is no currency column to say so. Copied onto each
    /// month's charge when it is raised rather than read fresh every time,
    /// so a price change never moves a charge already on the books.
    /// </summary>
    public decimal MonthlyFeeBob { get; set; }

    public DateOnly StartsOn { get; set; }

    /// <summary>When the course finishes. Null for one still running with no end set.</summary>
    public DateOnly? EndsOn { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Sport? Sport { get; set; }
    public VenueSpace? VenueSpace { get; set; }
    public OrganizationMembership? CoachMembership { get; set; }

    public ICollection<CourseSchedule> Schedules { get; set; } = [];
}

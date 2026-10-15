namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// One month's pension, for one enrollment.
/// </summary>
/// <remarks>
/// Carries no status of its own —
/// <see cref="SportFrog.Domain.School.TuitionLedger"/>, the domain
/// project's own type, is the one place that decides
/// Pending/Partial/Paid/Waived/Voided, computed fresh from this row's
/// own <see cref="AmountBob"/> against whatever counts as paid, every time
/// it is asked. A status column here would let somebody write "pagada" by
/// hand and leave it there after a payment was later voided from it —
/// exactly the drift a derived value cannot have.
///
/// <see cref="WaivedAt"/>/<see cref="VoidedAt"/> are independent
/// toggles, not one "struck" flag: a charge can be waived and later
/// voided outright, and <c>TuitionLedger</c> already resolves that
/// combination — voided outranks waived — without this entity needing
/// to forbid it.
/// </remarks>
public sealed class TuitionCharge
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid EnrollmentId { get; set; }

    /// <summary>Always the first of its month — see this entity's migration for the check that holds it to that.</summary>
    public DateOnly Period { get; set; }

    /// <summary>
    /// What this charge asks for, in bolivianos — copied from whichever of
    /// <see cref="CourseEnrollment.FeeOverrideBob"/> or
    /// <see cref="Course.MonthlyFeeBob"/> was in force the moment
    /// <c>TuitionAccrual</c> raised it. Never read fresh afterward: a
    /// price change, or a scholarship granted later, must not reach back
    /// and move a charge already on the books.
    /// </summary>
    public decimal AmountBob { get; set; }

    public DateOnly DueOn { get; set; }

    public DateTimeOffset? WaivedAt { get; set; }
    public string? WaiverReason { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }
    public string? VoidReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CourseEnrollment? Enrollment { get; set; }
}

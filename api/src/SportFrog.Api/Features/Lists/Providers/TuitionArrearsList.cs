using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>Every pension charge past its due date and still owed, organization-wide.</summary>
/// <remarks>
/// Scoped to the whole organization rather than to one course, the one
/// other provider in this feature for which that is true besides
/// <see cref="AthletesList"/> — collecting on an overdue pension is not a
/// course's own concern, the same reasoning <c>RecordPayment</c> and the
/// rest of <c>Features.Tuition</c> already follow for taking no course
/// scope at all. A membership scoped to specific courses still only sees
/// those courses' own arrears — <see cref="ReadLists"/> narrows this
/// provider's rows the same way it already narrows <see cref="AthletesList"/>'s.
///
/// "Owed" is resolved the one way <see cref="TuitionLedger.Resolve"/>
/// defines it — only payments neither voided nor rejected count — so this
/// can never list a charge as overdue that <c>ReadTuitionAccount</c> would
/// show as already settled. "Past due" is read against <see cref="clock"/>
/// rather than the database's own now(), so a test can fix what today is
/// the same way <c>TuitionAccrual</c>'s own callers already do.
/// </remarks>
internal sealed class TuitionArrearsList(TimeProvider clock) : IListProvider
{
    private static readonly IReadOnlyList<ListColumn> Columns =
    [
        new ListColumn("Curso", ListValueKind.Text),
        new ListColumn("Documento", ListValueKind.Text),
        new ListColumn("Apellido y nombre", ListValueKind.Text),
        new ListColumn("Período", ListValueKind.Date),
        new ListColumn("Monto (Bs)", ListValueKind.Number),
        new ListColumn("Pagado (Bs)", ListValueKind.Number),
        new ListColumn("Saldo (Bs)", ListValueKind.Number),
        new ListColumn("Vencimiento", ListValueKind.Date),
        new ListColumn("Días de mora", ListValueKind.Number),
    ];

    public string Slug => "morosos";

    public string Label => "Pensiones en mora";

    public IReadOnlyList<ListParameter> Parameters { get; } = [];

    // Organization-wide and about money owed, not about any sport or
    // score mode — same reasoning AthletesList gives for its own true.
    public Task<bool> AppliesToAsync(
        string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<ListTable?> LoadAsync(
        ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken) =>
        LoadAsync(scope, scopedCourseIds: null, database, cancellationToken);

    /// <summary>
    /// The same list, narrowed to charges whose course is one of
    /// <paramref name="scopedCourseIds"/> when given.
    /// </summary>
    /// <remarks>
    /// This provider declares no <see cref="ListParameterKind.Course"/>
    /// parameter of its own — the same case <see cref="AthletesList"/> is
    /// for <see cref="ListParameterKind.Category"/> — so <c>ReadLists</c>
    /// calls this overload directly for a scoped membership, passing
    /// <see cref="OrganizationContext.ScopedCourseIds"/> rather than
    /// refusing the whole list. Null means organization-wide; an empty set
    /// (a membership scoped to clubs or competitions only, never to a
    /// course) correctly narrows to nobody.
    /// </remarks>
    internal async Task<ListTable?> LoadAsync(
        ListScope scope,
        IReadOnlySet<Guid>? scopedCourseIds,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var overdue = await (
                from charge in database.TuitionCharges
                join entry in database.CourseEnrollments on charge.EnrollmentId equals entry.Id
                join athlete in database.Athletes on entry.AthleteId equals athlete.Id
                join course in database.Courses on entry.CourseId equals course.Id
                where charge.WaivedAt == null && charge.VoidedAt == null
                where charge.DueOn < today
                where scopedCourseIds == null || scopedCourseIds.Contains(course.Id)
                orderby charge.DueOn
                select new
                {
                    charge.Id,
                    charge.AmountBob,
                    charge.Period,
                    charge.DueOn,
                    athlete.DocumentId,
                    athlete.FirstName,
                    athlete.LastName,
                    CourseName = course.Name,
                })
            .ToListAsync(cancellationToken);

        if (overdue.Count == 0)
        {
            return new ListTable("Pensiones en mora", null, Columns, [new ListSection(null, [])]);
        }

        var chargeIds = overdue.Select(charge => charge.Id).ToList();

        var paymentsByCharge = (await database.TuitionPayments
                .Where(payment => chargeIds.Contains(payment.ChargeId))
                .ToListAsync(cancellationToken))
            .GroupBy(payment => payment.ChargeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rows = new List<IReadOnlyList<object?>>();

        foreach (var charge in overdue)
        {
            paymentsByCharge.TryGetValue(charge.Id, out var ownPayments);

            // Only what still counts toward the balance -- see
            // ReadTuitionAccount's own remarks for what excludes a payment.
            var counted = (ownPayments ?? [])
                .Where(payment => payment.VoidedAt is null
                    && payment.ReviewState != ReceiptReviewState.Rejected)
                .Select(payment => payment.AmountBob)
                .ToList();

            var ledger = TuitionLedger.Resolve(
                charge.AmountBob, waived: false, voided: false, counted);

            // Already settled despite the due date having passed -- not
            // arrears, whatever the calendar says.
            if (ledger.Status is not (ChargeStatus.Pending or ChargeStatus.Partial))
            {
                continue;
            }

            rows.Add(
            [
                charge.CourseName,
                charge.DocumentId,
                $"{charge.LastName}, {charge.FirstName}",
                charge.Period,
                ledger.Charged,
                ledger.Paid,
                ledger.Balance,
                charge.DueOn,
                today.DayNumber - charge.DueOn.DayNumber,
            ]);
        }

        return new ListTable("Pensiones en mora", null, Columns, [new ListSection(null, rows)]);
    }
}

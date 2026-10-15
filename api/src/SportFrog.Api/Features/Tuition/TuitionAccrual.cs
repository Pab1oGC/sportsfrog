using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Tuition;

/// <summary>What one accrual run for one organization and one period did.</summary>
public sealed record AccrualResult(int Charged, int AlreadyCharged, DateOnly Period);

/// <summary>
/// Raises one month's pension for every enrollment it is owed by.
/// </summary>
/// <remarks>
/// Shared between <see cref="AccrueTuition"/> (an administrator asking for
/// one organization, right now) and <see cref="TuitionAccrualJob"/> (every
/// organization, once a month, nobody asking): the two differ only in who
/// calls and how often, never in what counts as owed.
///
/// Idempotent by construction — <see cref="RunAsync"/> only ever inserts
/// charges for enrollments the period does not already have one for — so
/// calling it twice for the same organization and period, whether by a
/// person retrying or a schedule overlapping a manual run, changes nothing
/// the second time.
/// </remarks>
internal sealed class TuitionAccrual(SportFrogDbContext database)
{
    /// <summary>
    /// Raises <paramref name="period"/>'s pension for <paramref name="organizationId"/>.
    /// </summary>
    /// <remarks>
    /// An enrollment owes this period when it was already enrolled by the
    /// period's last day and, if it later left, did not leave before the
    /// period's first — the same "active as of a date" question
    /// <c>AttendancePolicy.ValidEnrollmentIdsAsync</c> asks, and the same
    /// no-proration rule an enrollment starting mid-month already pays in
    /// full by (see <see cref="Courses.Enrollments.EnrollmentPolicy"/>'s
    /// own remarks): a withdrawal mid-month owes the same whole month it
    /// started in, symmetrically.
    ///
    /// The course itself has to be running in the period too — a course
    /// that had not started yet, or had already ended, owes nothing for a
    /// month it was never open in, whatever its enrollments otherwise say.
    /// </remarks>
    public async Task<AccrualResult> RunAsync(
        Guid organizationId,
        DateOnly period,
        CancellationToken cancellationToken)
    {
        var periodStart = new DateOnly(period.Year, period.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var periodStartUtc = new DateTimeOffset(periodStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var periodEndExclusiveUtc = periodStartUtc.AddMonths(1);

        var candidates = await database.CourseEnrollments
            .Where(entry => entry.EnrolledAt < periodEndExclusiveUtc)
            .Where(entry => entry.WithdrawnAt == null || entry.WithdrawnAt >= periodStartUtc)
            .Where(entry => entry.Course!.StartsOn <= periodEnd)
            .Where(entry => entry.Course!.EndsOn == null || entry.Course!.EndsOn >= periodStart)
            .Select(entry => new { entry.Id, Amount = entry.FeeOverrideBob ?? entry.Course!.MonthlyFeeBob })
            .ToListAsync(cancellationToken);

        var existing = (await database.TuitionCharges
                .Where(charge => charge.Period == periodStart)
                .Select(charge => charge.EnrollmentId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var toCreate = candidates.Where(candidate => !existing.Contains(candidate.Id)).ToList();

        // Added to the set directly, not through a navigation collection —
        // see UpdateCourse's own remark on why a freshly assigned key
        // reaching the tracker only through a parent's collection is read
        // back as "this already exists".
        foreach (var candidate in toCreate)
        {
            database.TuitionCharges.Add(new TuitionCharge
            {
                Id = Guid.NewGuid(),
                OrgId = organizationId,
                EnrollmentId = candidate.Id,
                Period = periodStart,
                AmountBob = candidate.Amount,

                // Due by the end of the month it covers — stated here as
                // the one assumption this makes, since nothing names a
                // different rule.
                DueOn = periodEnd,
            });
        }

        await database.SaveChangesAsync(cancellationToken);

        return new AccrualResult(toCreate.Count, candidates.Count - toCreate.Count, periodStart);
    }
}

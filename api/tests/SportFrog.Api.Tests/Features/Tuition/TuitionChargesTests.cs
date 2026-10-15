using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Tuition;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.School;

namespace SportFrog.Api.Tests.Features.Tuition;

/// <summary>
/// Raising a month's pension, reading it back as a ledger, and correcting a
/// charge once it exists. The risk worth pinning is
/// <see cref="TuitionAccrual"/>'s own job — which enrollments a period owes
/// and which it does not — since <c>TuitionLedger</c> itself is already
/// proven at the domain level (see <c>TuitionLedgerTests</c>).
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class TuitionChargesTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    // The period every "current month" test accrues for.
    private static readonly DateOnly March = new(2026, 3, 1);
    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // See AttendanceTests' own remark: DateOnly.ToDateTime(...) reads as the
    // machine's local offset, and Npgsql's timestamptz only accepts UTC.
    private static DateTimeOffset AtUtc(DateOnly date, int hour = 0) =>
        new(date.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);

    private static OrganizationContext OrganizationFor(Guid orgId)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Admin, userId: Guid.NewGuid(), ipAddress: null);
        return organization;
    }

    private async Task<Guid> SeedOrganizationAsync()
    {
        var orgId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        await setup.SaveChangesAsync();

        return orgId;
    }

    /// <summary>One course and one enrollment — the shared shape every test starts from.</summary>
    private sealed record Fixture(Guid OrgId, Guid CourseId, Guid EnrollmentId);

    private async Task<Fixture> SeedAsync(
        decimal monthlyFeeBob = 100m,
        decimal? feeOverrideBob = null,
        DateOnly? courseStartsOn = null,
        DateOnly? courseEndsOn = null,
        DateTimeOffset? enrolledAt = null,
        DateTimeOffset? withdrawnAt = null)
    {
        var orgId = await SeedOrganizationAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var courseId = Guid.NewGuid();
        context.Courses.Add(new Course
        {
            Id = courseId,
            OrgId = orgId,
            SportCode = Football,
            Name = $"Curso {Guid.NewGuid():N}",
            MonthlyFeeBob = monthlyFeeBob,
            StartsOn = courseStartsOn ?? new DateOnly(2026, 1, 1),
            EndsOn = courseEndsOn,
        });

        var athleteId = Guid.NewGuid();
        context.Athletes.Add(new Athlete
        {
            Id = athleteId,
            OrgId = orgId,
            FirstName = "Estudiante",
            LastName = "DePrueba",
            DocumentId = $"doc-{athleteId:N}",
            BirthDate = new DateOnly(2015, 1, 1),
        });

        var enrollmentId = Guid.NewGuid();
        context.CourseEnrollments.Add(new CourseEnrollment
        {
            Id = enrollmentId,
            OrgId = orgId,
            CourseId = courseId,
            AthleteId = athleteId,
            EnrolledAt = enrolledAt ?? AtUtc(new DateOnly(2026, 1, 1)),
            WithdrawnAt = withdrawnAt,
            FeeOverrideBob = feeOverrideBob,
            FeeOverrideReason = feeOverrideBob.HasValue ? "Beca de prueba" : null,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, courseId, enrollmentId);
    }

    private async Task<IResult> AccrueAsync(Guid orgId, int year, int month)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await AccrueTuition.HandleAsync(
            new AccrueTuition.Request(year, month),
            new TuitionAccrual(context),
            OrganizationFor(orgId),
            Clock,
            CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<AccrueTuition.Response> AccrueSuccessfullyAsync(Guid orgId, int year, int month) =>
        Assert.IsType<Ok<AccrueTuition.Response>>(await AccrueAsync(orgId, year, month)).Value!;

    private async Task<List<ReadTuitionAccount.Charge>> ReadAccountAsync(Guid orgId, Guid enrollmentId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReadTuitionAccount.ListAsync(enrollmentId, context, CancellationToken.None);

        return Assert.IsType<Ok<List<ReadTuitionAccount.Charge>>>(result).Value!;
    }

    private async Task<IResult> WaiveAsync(Guid orgId, Guid chargeId, string? reason)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await WaiveCharge.HandleAsync(
            chargeId, new WaiveCharge.Request(reason), context, Clock, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> VoidAsync(Guid orgId, Guid chargeId, string? reason)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await VoidCharge.HandleAsync(
            chargeId, new VoidCharge.Request(reason), context, Clock, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    [Fact]
    public async Task Accrue_OneActiveEnrollment_ChargesTheCoursesMonthlyFee()
    {
        var fx = await SeedAsync(monthlyFeeBob: 150m);

        var response = await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);

        response.Charged.Should().Be(1);
        response.AlreadyCharged.Should().Be(0);

        var account = await ReadAccountAsync(fx.OrgId, fx.EnrollmentId);
        account.Should().ContainSingle(charge =>
            charge.Period == March && charge.AmountBob == 150m && charge.Status == ChargeStatus.Pending);
    }

    [Fact]
    public async Task Accrue_SamePeriodTwice_IsIdempotent()
    {
        var fx = await SeedAsync();

        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);
        var second = await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);

        second.Charged.Should().Be(0);
        second.AlreadyCharged.Should().Be(1);

        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Accrue_EnrollmentWithFeeOverride_ChargesTheOverrideNotTheCoursePrice()
    {
        var fx = await SeedAsync(monthlyFeeBob: 150m, feeOverrideBob: 0m);

        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);

        var account = await ReadAccountAsync(fx.OrgId, fx.EnrollmentId);
        account.Should().ContainSingle(charge => charge.AmountBob == 0m);
    }

    [Fact]
    public async Task Accrue_AFuturePeriod_ReturnsConflict()
    {
        var fx = await SeedAsync();

        var result = await AccrueAsync(fx.OrgId, March.Year, March.Month + 1);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Accrue_EnrollmentWithdrawnBeforeThePeriodStarted_IsNotCharged()
    {
        var fx = await SeedAsync(withdrawnAt: AtUtc(March.AddDays(-1)));

        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);

        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Accrue_EnrollmentWithdrawnOnThePeriodsFirstDay_StillOwesTheWholeMonth()
    {
        // The no-proration rule, symmetrically: leaving on the period's own
        // first day still owes that whole month, the same as starting on it
        // already pays it in full.
        var fx = await SeedAsync(withdrawnAt: AtUtc(March));

        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);

        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Accrue_EnrolledOnlyAfterThePeriodEnded_IsNotCharged()
    {
        var fx = await SeedAsync(enrolledAt: AtUtc(March.AddMonths(1)));

        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);

        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Accrue_EnrolledOnThePeriodsLastDay_IsStillCharged()
    {
        var fx = await SeedAsync(enrolledAt: AtUtc(March.AddMonths(1).AddDays(-1)));

        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);

        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Accrue_CourseNotYetStartedInThePeriod_IsNotCharged()
    {
        var fx = await SeedAsync(courseStartsOn: March.AddMonths(1));

        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);

        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Accrue_CourseAlreadyEndedBeforeThePeriod_IsNotCharged()
    {
        var fx = await SeedAsync(courseEndsOn: March.AddMonths(-1));

        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);

        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Waive_ThenClear_RoundTrips()
    {
        var fx = await SeedAsync();
        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);
        var chargeId = (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Single().Id;

        Assert.IsType<NoContent>(await WaiveAsync(fx.OrgId, chargeId, "Beca otorgada después"));
        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Single().Status.Should().Be(ChargeStatus.Waived);

        Assert.IsType<NoContent>(await WaiveAsync(fx.OrgId, chargeId, null));
        var cleared = (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Single();
        cleared.Status.Should().Be(ChargeStatus.Pending);
        cleared.WaivedAt.Should().BeNull();
        cleared.WaiverReason.Should().BeNull();
    }

    [Fact]
    public async Task Void_ThenClear_RoundTrips()
    {
        var fx = await SeedAsync();
        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);
        var chargeId = (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Single().Id;

        Assert.IsType<NoContent>(await VoidAsync(fx.OrgId, chargeId, "El motor no debía haberla generado"));
        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Single().Status.Should().Be(ChargeStatus.Voided);

        Assert.IsType<NoContent>(await VoidAsync(fx.OrgId, chargeId, null));
        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Single().Status.Should().Be(ChargeStatus.Pending);
    }

    [Fact]
    public async Task Void_OutranksWaiver_WhenACharge_IsBoth()
    {
        var fx = await SeedAsync();
        await AccrueSuccessfullyAsync(fx.OrgId, March.Year, March.Month);
        var chargeId = (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Single().Id;

        await WaiveAsync(fx.OrgId, chargeId, "Beca");
        await VoidAsync(fx.OrgId, chargeId, "Generada por error");

        (await ReadAccountAsync(fx.OrgId, fx.EnrollmentId)).Single().Status.Should().Be(ChargeStatus.Voided);
    }

    [Fact]
    public async Task Waive_UnknownCharge_ReturnsNotFound()
    {
        var orgId = await SeedOrganizationAsync();

        var result = await WaiveAsync(orgId, Guid.NewGuid(), "Motivo");

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task ReadAccount_UnknownEnrollment_ReturnsNotFound()
    {
        var orgId = await SeedOrganizationAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReadTuitionAccount.ListAsync(Guid.NewGuid(), context, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public void AccrueValidator_MonthOutOfRange_IsRejected()
    {
        var result = new AccrueTuition.Validator().Validate(new AccrueTuition.Request(2026, 13));

        result.IsValid.Should().BeFalse();
    }
}

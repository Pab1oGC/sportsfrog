using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>
/// Whether <see cref="TuitionArrearsList"/> actually reads overdue, unpaid
/// charges across the whole organization — a query that compiles can still
/// fail the moment EF Core tries to translate it, which only shows up
/// against a real database. <see cref="SportFrog.Domain.School.TuitionLedger"/>
/// itself is already proven at the domain level.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class TuitionArrearsListTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    // "Today", fixed, so "overdue" is a question with one stable answer
    // regardless of when this test actually runs.
    private static readonly DateOnly Today = new(2026, 3, 10);
    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(Today.Year, Today.Month, Today.Day, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record Fixture(Guid OrgId, Guid OverdueAthleteId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>One course, one charge per settlement state a charge can be in — only the plain, unpaid, overdue one should ever read as arrears.</summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Courses.Add(new Course
        {
            Id = courseId, OrgId = orgId, SportCode = Football, Name = "Fútbol infantil",
            MonthlyFeeBob = 100m, StartsOn = new DateOnly(2026, 1, 1),
        });

        var overdueAthleteId = await SeedEnrollmentWithChargeAsync(
            setup, orgId, courseId, "Diaz", waived: false, voided: false,
            dueOn: Today.AddDays(-10), payments: []);

        await SeedEnrollmentWithChargeAsync(
            setup, orgId, courseId, "Pagada", waived: false, voided: false,
            dueOn: Today.AddDays(-10), payments: [(100m, false)]);

        await SeedEnrollmentWithChargeAsync(
            setup, orgId, courseId, "Condonada", waived: true, voided: false,
            dueOn: Today.AddDays(-10), payments: []);

        await SeedEnrollmentWithChargeAsync(
            setup, orgId, courseId, "Anulada", waived: false, voided: true,
            dueOn: Today.AddDays(-10), payments: []);

        await SeedEnrollmentWithChargeAsync(
            setup, orgId, courseId, "Futura", waived: false, voided: false,
            dueOn: Today.AddDays(10), payments: []);

        await SeedEnrollmentWithChargeAsync(
            setup, orgId, courseId, "Rechazada", waived: false, voided: false,
            dueOn: Today.AddDays(-10), payments: [(100m, true)]);

        await transaction.CommitAsync();

        return new Fixture(orgId, overdueAthleteId);
    }

    /// <summary>
    /// One enrollment and one overdue-dated charge, with an optional list of
    /// payments — each a (amount, rejected) pair — against it.
    /// </summary>
    private static async Task<Guid> SeedEnrollmentWithChargeAsync(
        SportFrogDbContext context,
        Guid orgId,
        Guid courseId,
        string lastName,
        bool waived,
        bool voided,
        DateOnly dueOn,
        IReadOnlyList<(decimal Amount, bool Rejected)> payments)
    {
        var athleteId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var chargeId = Guid.NewGuid();

        context.Athletes.Add(new Athlete
        {
            Id = athleteId, OrgId = orgId, FirstName = "Estudiante", LastName = lastName,
            DocumentId = $"doc-{athleteId:N}", BirthDate = new DateOnly(2015, 1, 1),
        });
        context.CourseEnrollments.Add(new CourseEnrollment
        {
            Id = enrollmentId, OrgId = orgId, CourseId = courseId, AthleteId = athleteId,
            EnrolledAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });

        await context.SaveChangesAsync();

        context.TuitionCharges.Add(new TuitionCharge
        {
            Id = chargeId, OrgId = orgId, EnrollmentId = enrollmentId,
            Period = new DateOnly(2026, 1, 1), AmountBob = 100m, DueOn = dueOn,
            WaivedAt = waived ? DateTimeOffset.UtcNow : null,
            WaiverReason = waived ? "Beca de prueba" : null,
            VoidedAt = voided ? DateTimeOffset.UtcNow : null,
            VoidReason = voided ? "Anulado de prueba" : null,
        });

        await context.SaveChangesAsync();

        foreach (var (amount, rejected) in payments)
        {
            context.TuitionPayments.Add(new TuitionPayment
            {
                Id = Guid.NewGuid(), OrgId = orgId, ChargeId = chargeId, AmountBob = amount,
                Method = PaymentMethod.Cash,
                ReviewState = rejected ? ReceiptReviewState.Rejected : ReceiptReviewState.Verified,
                ReviewReason = rejected ? "Comprobante inválido" : null,
            });
        }

        await context.SaveChangesAsync();

        return athleteId;
    }

    private async Task<Session> OpenAsync(Guid orgId)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        return new Session(context, transaction);
    }

    private static async Task<List<IReadOnlyList<object?>>> RowsAsync(Session session) =>
        (await new TuitionArrearsList(Clock).LoadAsync(ListScope.Empty, session.Context, CancellationToken.None))!
            .Sections.Single().Rows.ToList();

    [Fact]
    public async Task LoadAsync_PlainUnpaidOverdueCharge_Appears()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var rows = await RowsAsync(session);

        rows.Should().ContainSingle(row => row[2]!.Equals("Diaz, Estudiante"));
    }

    [Fact]
    public async Task LoadAsync_FullyPaidCharge_DoesNotAppearEvenThoughOverdue()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var rows = await RowsAsync(session);

        rows.Should().NotContain(row => row[2]!.Equals("Pagada, Estudiante"));
    }

    [Fact]
    public async Task LoadAsync_WaivedCharge_DoesNotAppearEvenThoughOverdue()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var rows = await RowsAsync(session);

        rows.Should().NotContain(row => row[2]!.Equals("Condonada, Estudiante"));
    }

    [Fact]
    public async Task LoadAsync_VoidedCharge_DoesNotAppearEvenThoughOverdue()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var rows = await RowsAsync(session);

        rows.Should().NotContain(row => row[2]!.Equals("Anulada, Estudiante"));
    }

    [Fact]
    public async Task LoadAsync_NotYetDueCharge_DoesNotAppear()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var rows = await RowsAsync(session);

        rows.Should().NotContain(row => row[2]!.Equals("Futura, Estudiante"));
    }

    [Fact]
    public async Task LoadAsync_RejectedPaymentDoesNotCount_ChargeStillAppears()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var rows = await RowsAsync(session);

        var row = rows.Single(row => row[2]!.Equals("Rechazada, Estudiante"));
        row[4].Should().Be(100m); // amount
        row[5].Should().Be(0m); // paid -- the rejected payment never counted
        row[6].Should().Be(100m); // balance
    }

    [Fact]
    public async Task LoadAsync_NoOverdueCharges_RendersAnEmptyTableRatherThanNull()
    {
        var orgId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var session = await OpenAsync(orgId);

        var table = await new TuitionArrearsList(Clock).LoadAsync(
            ListScope.Empty, session.Context, CancellationToken.None);

        table.Should().NotBeNull();
        table!.Sections.Single().Rows.Should().BeEmpty();
    }

    /// <summary>Two courses, each with one plain overdue charge — enough to pin narrowing by scoped course ids.</summary>
    private sealed record TwoCoursesFixture(Guid OrgId, Guid CourseAId, Guid CourseBId);

    private async Task<TwoCoursesFixture> SeedTwoCoursesAsync()
    {
        var orgId = Guid.NewGuid();
        var courseAId = Guid.NewGuid();
        var courseBId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Courses.Add(new Course
        {
            Id = courseAId, OrgId = orgId, SportCode = Football, Name = "Curso A",
            MonthlyFeeBob = 100m, StartsOn = new DateOnly(2026, 1, 1),
        });
        setup.Courses.Add(new Course
        {
            Id = courseBId, OrgId = orgId, SportCode = Football, Name = "Curso B",
            MonthlyFeeBob = 100m, StartsOn = new DateOnly(2026, 1, 1),
        });

        await SeedEnrollmentWithChargeAsync(
            setup, orgId, courseAId, "DeA", waived: false, voided: false,
            dueOn: Today.AddDays(-10), payments: []);
        await SeedEnrollmentWithChargeAsync(
            setup, orgId, courseBId, "DeB", waived: false, voided: false,
            dueOn: Today.AddDays(-10), payments: []);

        await transaction.CommitAsync();

        return new TwoCoursesFixture(orgId, courseAId, courseBId);
    }

    [Fact]
    public async Task LoadAsync_NoScopedCourses_ListsBothCoursesArrearsAsUnscopedDoes()
    {
        var fx = await SeedTwoCoursesAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var table = await new TuitionArrearsList(Clock).LoadAsync(
            ListScope.Empty, scopedCourseIds: null, session.Context, CancellationToken.None);

        table!.Sections.Single().Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_ScopedToOneCourse_NarrowsToItsOwnArrears()
    {
        var fx = await SeedTwoCoursesAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var table = await new TuitionArrearsList(Clock).LoadAsync(
            ListScope.Empty, new HashSet<Guid> { fx.CourseAId }, session.Context, CancellationToken.None);

        var rows = table!.Sections.Single().Rows;
        rows.Should().ContainSingle();
        rows.Single()[2].Should().Be("DeA, Estudiante");
    }

    [Fact]
    public async Task LoadAsync_ScopedToNoCourseAtAll_ExcludesEveryone()
    {
        // A membership scoped to clubs or competitions only, never to a
        // course: the empty set correctly narrows to nobody, not to
        // everybody.
        var fx = await SeedTwoCoursesAsync();
        await using var session = await OpenAsync(fx.OrgId);

        var table = await new TuitionArrearsList(Clock).LoadAsync(
            ListScope.Empty, new HashSet<Guid>(), session.Context, CancellationToken.None);

        table!.Sections.Single().Rows.Should().BeEmpty();
    }
}

using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.School;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>Whether <see cref="AttendanceList"/> actually reads a course's attendance records — a
/// query that compiles can still fail the moment EF Core tries to translate it, which only shows
/// up against a real database. <see cref="AttendanceRate"/> itself is already proven at the
/// domain level (see <c>AttendanceRateTests</c>).</summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class AttendanceListTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    private sealed record Fixture(Guid OrgId, Guid CourseId, Guid MarkedEnrollmentId, Guid UnmarkedEnrollmentId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>
    /// One course with two held sessions: one student marked present on
    /// both, one never marked at all — enough to pin a real rate against
    /// the "nothing recorded yet" null.
    /// </summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var markedAthleteId = Guid.NewGuid();
        var unmarkedAthleteId = Guid.NewGuid();
        var sessionAId = Guid.NewGuid();
        var sessionBId = Guid.NewGuid();
        var markedEnrollmentId = Guid.NewGuid();
        var unmarkedEnrollmentId = Guid.NewGuid();

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

        setup.Athletes.Add(new Athlete
        {
            Id = markedAthleteId, OrgId = orgId, FirstName = "Juan", LastName = "Diaz",
            DocumentId = $"doc-{markedAthleteId:N}", BirthDate = new DateOnly(2015, 1, 1),
        });
        setup.Athletes.Add(new Athlete
        {
            Id = unmarkedAthleteId, OrgId = orgId, FirstName = "Ana", LastName = "Soto",
            DocumentId = $"doc-{unmarkedAthleteId:N}", BirthDate = new DateOnly(2014, 6, 1),
        });

        setup.CourseSessions.Add(new CourseSession
        {
            Id = sessionAId, OrgId = orgId, CourseId = courseId,
            SessionDate = new DateOnly(2026, 2, 3), StartsAt = new TimeOnly(18, 0), EndsAt = new TimeOnly(19, 0),
            Status = CourseSessionState.Held,
        });
        setup.CourseSessions.Add(new CourseSession
        {
            Id = sessionBId, OrgId = orgId, CourseId = courseId,
            SessionDate = new DateOnly(2026, 2, 10), StartsAt = new TimeOnly(18, 0), EndsAt = new TimeOnly(19, 0),
            Status = CourseSessionState.Held,
        });

        await setup.SaveChangesAsync();

        setup.CourseEnrollments.Add(new CourseEnrollment
        {
            Id = markedEnrollmentId, OrgId = orgId, CourseId = courseId, AthleteId = markedAthleteId,
            EnrolledAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });
        setup.CourseEnrollments.Add(new CourseEnrollment
        {
            Id = unmarkedEnrollmentId, OrgId = orgId, CourseId = courseId, AthleteId = unmarkedAthleteId,
            EnrolledAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });

        await setup.SaveChangesAsync();

        setup.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(), OrgId = orgId, SessionId = sessionAId, EnrollmentId = markedEnrollmentId,
            Status = AttendanceState.Present,
        });
        setup.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(), OrgId = orgId, SessionId = sessionBId, EnrollmentId = markedEnrollmentId,
            Status = AttendanceState.Late,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, courseId, markedEnrollmentId, unmarkedEnrollmentId);
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    [Fact]
    public async Task LoadAsync_UnknownCourse_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AttendanceList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["courseId"] = Guid.NewGuid().ToString() }),
            session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_NoCourseIdInScope_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AttendanceList().LoadAsync(ListScope.Empty, session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_KnownCourse_TitleNamesTheCourse()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AttendanceList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["courseId"] = fx.CourseId.ToString() }),
            session.Context, CancellationToken.None);

        table!.Title.Should().Be("Asistencia — Fútbol infantil");
    }

    [Fact]
    public async Task LoadAsync_MarkedOnBothSessions_ComputesTheRate()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AttendanceList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["courseId"] = fx.CourseId.ToString() }),
            session.Context, CancellationToken.None);

        var row = table!.Sections.Single().Rows.Single(row => row[1]!.Equals("Diaz, Juan"));
        row[2].Should().Be(2); // recorded
        row[3].Should().Be(1); // present
        row[4].Should().Be(1); // late
        row[7].Should().Be(100.0); // present and late both count toward the rate
    }

    [Fact]
    public async Task LoadAsync_NeverMarked_HasANullRateRatherThanZero()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new AttendanceList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["courseId"] = fx.CourseId.ToString() }),
            session.Context, CancellationToken.None);

        var row = table!.Sections.Single().Rows.Single(row => row[1]!.Equals("Soto, Ana"));
        row[2].Should().Be(0);
        row[7].Should().BeNull();
    }
}

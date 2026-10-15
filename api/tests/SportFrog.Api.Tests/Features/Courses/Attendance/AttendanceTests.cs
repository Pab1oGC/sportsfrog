using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Courses.Attendance;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.School;

namespace SportFrog.Api.Tests.Features.Courses.Attendance;

/// <summary>
/// Recording a session's sheet, reading it back, and the one computed
/// report that depends on both: a student's attendance rate across a
/// course. The risk worth pinning is <see cref="AttendancePolicy"/>'s own
/// job — which sessions are open to a mark and which enrollments a mark on
/// one may actually name — since <c>AttendanceRate</c> itself is already
/// proven at the domain level (see <c>AttendanceRateTests</c>).
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class AttendanceTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";
    // A Tuesday, matching this course's own weekly slot.
    private static readonly DateOnly Today = new(2026, 3, 10);
    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(Today.Year, Today.Month, Today.Day, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // DateOnly.ToDateTime(...) returns Kind=Unspecified, and an implicit
    // DateTime -> DateTimeOffset conversion reads that as the machine's own
    // local offset -- Npgsql accepts only Offset=0 for timestamptz. Built
    // explicitly in UTC here so a test run on a non-UTC machine still seeds
    // the same instant.
    private static DateTimeOffset AtUtc(DateOnly date, int hour = 0) =>
        new(date.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);

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

    /// <summary>A course, one enrollment, and one session on <see cref="Today"/> — the shared shape every test starts from.</summary>
    private sealed record Fixture(Guid OrgId, Guid CourseId, Guid EnrollmentId, Guid SessionId);

    private async Task<Fixture> SeedAsync(
        DateTimeOffset? enrolledAt = null,
        DateTimeOffset? withdrawnAt = null,
        CourseSessionState sessionStatus = CourseSessionState.Scheduled,
        DateOnly? sessionDate = null)
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
            MonthlyFeeBob = 100m,
            StartsOn = new DateOnly(2026, 1, 1),
        });
        context.CourseSchedules.Add(new CourseSchedule
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            CourseId = courseId,
            DayOfWeek = DayOfWeek.Tuesday,
            StartsAt = new TimeOnly(18, 0),
            EndsAt = new TimeOnly(19, 0),
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
            EnrolledAt = enrolledAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            WithdrawnAt = withdrawnAt,
        });

        var sessionId = Guid.NewGuid();
        context.CourseSessions.Add(new CourseSession
        {
            Id = sessionId,
            OrgId = orgId,
            CourseId = courseId,
            SessionDate = sessionDate ?? Today,
            StartsAt = new TimeOnly(18, 0),
            EndsAt = new TimeOnly(19, 0),
            Status = sessionStatus,
            CancellationReason = sessionStatus == CourseSessionState.Cancelled ? "Lluvia" : null,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, courseId, enrollmentId, sessionId);
    }

    private async Task<IResult> RecordAsync(Guid orgId, Guid sessionId, params RecordAttendance.Mark[] marks)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await RecordAttendance.HandleAsync(
            sessionId, new RecordAttendance.Request(marks), context, new AttendancePolicy(context, Clock),
            CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<List<ReadSessionAttendance.Summary>> ReadSheetAsync(Guid orgId, Guid sessionId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReadSessionAttendance.ListAsync(
            sessionId, context, new AttendancePolicy(context, Clock), CancellationToken.None);

        return Assert.IsType<Ok<List<ReadSessionAttendance.Summary>>>(result).Value!;
    }

    private async Task<List<ReadAttendanceSummary.Summary>> ReadSummaryAsync(Guid orgId, Guid courseId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReadAttendanceSummary.ListAsync(courseId, context, CancellationToken.None);

        return Assert.IsType<Ok<List<ReadAttendanceSummary.Summary>>>(result).Value!;
    }

    private async Task<CourseSession> ReadSessionStatusAsync(Guid orgId, Guid sessionId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        return await context.CourseSessions.AsNoTracking().SingleAsync(session => session.Id == sessionId);
    }

    [Fact]
    public async Task RecordThenRead_RoundTrips()
    {
        var fx = await SeedAsync();

        var result = await RecordAsync(
            fx.OrgId, fx.SessionId, new RecordAttendance.Mark(fx.EnrollmentId, AttendanceState.Present, "Llegó a tiempo"));

        Assert.IsType<NoContent>(result);

        var sheet = await ReadSheetAsync(fx.OrgId, fx.SessionId);
        sheet.Should().ContainSingle(entry =>
            entry.EnrollmentId == fx.EnrollmentId
            && entry.Status == AttendanceState.Present
            && entry.Note == "Llegó a tiempo");
    }

    [Fact]
    public async Task RecordingASheet_MovesTheSessionFromScheduledToHeld()
    {
        var fx = await SeedAsync();

        (await ReadSessionStatusAsync(fx.OrgId, fx.SessionId)).Status.Should().Be(CourseSessionState.Scheduled);

        await RecordAsync(fx.OrgId, fx.SessionId, new RecordAttendance.Mark(fx.EnrollmentId, AttendanceState.Present, null));

        (await ReadSessionStatusAsync(fx.OrgId, fx.SessionId)).Status.Should().Be(CourseSessionState.Held);
    }

    [Fact]
    public async Task RecordingTwice_UpdatesTheSameMarkRatherThanDuplicatingIt()
    {
        var fx = await SeedAsync();

        await RecordAsync(fx.OrgId, fx.SessionId, new RecordAttendance.Mark(fx.EnrollmentId, AttendanceState.Absent, null));
        await RecordAsync(fx.OrgId, fx.SessionId, new RecordAttendance.Mark(fx.EnrollmentId, AttendanceState.Late, "Llegó tarde"));

        var sheet = await ReadSheetAsync(fx.OrgId, fx.SessionId);
        sheet.Should().ContainSingle(entry =>
            entry.EnrollmentId == fx.EnrollmentId
            && entry.Status == AttendanceState.Late
            && entry.Note == "Llegó tarde");
    }

    [Fact]
    public async Task Record_ACancelledSession_IsRefused()
    {
        var fx = await SeedAsync(sessionStatus: CourseSessionState.Cancelled);

        var result = await RecordAsync(fx.OrgId, fx.SessionId, new RecordAttendance.Mark(fx.EnrollmentId, AttendanceState.Present, null));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Record_ASessionInTheFuture_IsRefused()
    {
        var fx = await SeedAsync(sessionDate: Today.AddDays(7));

        var result = await RecordAsync(fx.OrgId, fx.SessionId, new RecordAttendance.Mark(fx.EnrollmentId, AttendanceState.Present, null));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Record_AnEnrollmentWithdrawnBeforeTheSessionsDate_IsRefused()
    {
        var fx = await SeedAsync(withdrawnAt: AtUtc(Today.AddDays(-1)));

        var result = await RecordAsync(fx.OrgId, fx.SessionId, new RecordAttendance.Mark(fx.EnrollmentId, AttendanceState.Present, null));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Record_AnEnrollmentWithdrawnOnTheSessionsOwnDate_IsStillAccepted()
    {
        // Withdrawn later the same day the class ran: they were still a
        // student when it happened.
        var fx = await SeedAsync(withdrawnAt: AtUtc(Today, hour: 20));

        var result = await RecordAsync(fx.OrgId, fx.SessionId, new RecordAttendance.Mark(fx.EnrollmentId, AttendanceState.Present, null));

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task Record_AnEnrollmentNotYetEnrolledAsOfTheSessionsDate_IsRefused()
    {
        var fx = await SeedAsync(enrolledAt: AtUtc(Today.AddDays(1)));

        var result = await RecordAsync(fx.OrgId, fx.SessionId, new RecordAttendance.Mark(fx.EnrollmentId, AttendanceState.Present, null));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Validator_TheSameEnrollmentMarkedTwice_IsRejected()
    {
        var enrollmentId = Guid.NewGuid();
        var request = new RecordAttendance.Request([
            new RecordAttendance.Mark(enrollmentId, AttendanceState.Present, null),
            new RecordAttendance.Mark(enrollmentId, AttendanceState.Absent, null),
        ]);

        var result = new RecordAttendance.Validator().Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReadSheet_AnUnmarkedStudent_HasANullStatusRatherThanBeingLeftOut()
    {
        var fx = await SeedAsync();

        var sheet = await ReadSheetAsync(fx.OrgId, fx.SessionId);

        sheet.Should().ContainSingle(entry => entry.EnrollmentId == fx.EnrollmentId && entry.Status == null);
    }

    [Fact]
    public async Task Summary_ASessionThatRanButWasNeverMarked_IsAGapNotAnAbsence()
    {
        var fx = await SeedAsync();

        // Held directly, with no attendance_records row for anyone --
        // RecordAttendance never leaves a session in this state (taking
        // any mark at all is what moves it to Held), but an operator's
        // missed afternoon is exactly the gap AttendanceRate exists to
        // tell apart from a real absence.
        await using (var context = fixture.CreateAppContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);
            context.CourseSessions.Add(new CourseSession
            {
                Id = Guid.NewGuid(),
                OrgId = fx.OrgId,
                CourseId = fx.CourseId,
                SessionDate = Today.AddDays(-7),
                StartsAt = new TimeOnly(18, 0),
                EndsAt = new TimeOnly(19, 0),
                Status = CourseSessionState.Held,
            });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await RecordAsync(fx.OrgId, fx.SessionId, new RecordAttendance.Mark(fx.EnrollmentId, AttendanceState.Present, null));

        var summary = await ReadSummaryAsync(fx.OrgId, fx.CourseId);

        var row = summary.Single(s => s.EnrollmentId == fx.EnrollmentId);
        row.Present.Should().Be(1);
        // The older, unmarked session does not reach Recorded, so the rate
        // reads 1/1, not 1/2.
        row.Recorded.Should().Be(1);
        row.Rate.Should().Be(1m);
    }

    [Fact]
    public async Task Summary_NoSessionsRecordedYet_HasANullRateRatherThanZero()
    {
        var fx = await SeedAsync();

        var summary = await ReadSummaryAsync(fx.OrgId, fx.CourseId);

        summary.Should().ContainSingle(s => s.EnrollmentId == fx.EnrollmentId && s.Rate == null && s.Recorded == 0);
    }
}

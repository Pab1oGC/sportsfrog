using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SportFrog.Api.Features.Courses;
using SportFrog.Api.Features.Courses.Sessions;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.School;

namespace SportFrog.Api.Tests.Features.Courses.Sessions;

/// <summary>
/// Turning a course's weekly schedule into dated sessions, and the two
/// write operations a dated session actually needs: cancelling one (a
/// reversible toggle, like withdrawing an enrollment) and moving one to
/// another date or time. The risk worth pinning here is the one
/// <see cref="GenerateSessions"/>'s own remarks call out: a course that
/// meets twice on the same day must get two sessions, not a unique-index
/// violation on the second.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CourseSessionsTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

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

    private async Task<Guid> SeedCourseAsync(
        Guid orgId,
        IReadOnlyList<WeeklySlot> schedules,
        DateOnly? startsOn = null,
        DateOnly? endsOn = null,
        bool isActive = true)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var contract = new CourseContract(
            SportCode: Football,
            Name: $"Curso {Guid.NewGuid():N}",
            VenueSpaceId: null,
            CoachMembershipId: null,
            Capacity: null,
            MonthlyFeeBob: 100m,
            StartsOn: startsOn ?? new DateOnly(2026, 1, 1),
            EndsOn: endsOn,
            IsActive: isActive,
            Schedules: schedules);

        var result = await CreateCourse.HandleAsync(contract, context, OrganizationFor(orgId), CancellationToken.None);
        await transaction.CommitAsync();

        return Assert.IsType<Created<CreateCourse.Response>>(result).Value!.Id;
    }

    private async Task<GenerateSessions.Response> GenerateAsync(Guid orgId, Guid courseId, DateOnly from, DateOnly to)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await GenerateSessions.HandleAsync(
            courseId, new GenerateSessions.Request(from, to), context, CancellationToken.None);
        await transaction.CommitAsync();

        return Assert.IsType<Ok<GenerateSessions.Response>>(result).Value!;
    }

    private async Task<IResult> GenerateRawAsync(Guid orgId, Guid courseId, DateOnly from, DateOnly to)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await GenerateSessions.HandleAsync(
            courseId, new GenerateSessions.Request(from, to), context, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<List<ReadSessions.Summary>> ListAsync(
        Guid orgId, Guid courseId, DateOnly? from = null, DateOnly? to = null)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReadSessions.ListAsync(courseId, context, CancellationToken.None, from, to);

        return Assert.IsType<Ok<List<ReadSessions.Summary>>>(result).Value!;
    }

    private async Task<IResult> CancelAsync(Guid orgId, Guid sessionId, bool cancelled, string? reason = "Lluvia")
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await CancelSession.HandleAsync(
            sessionId, new CancelSession.Request(cancelled, reason), context, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> RescheduleAsync(
        Guid orgId, Guid sessionId, DateOnly date, TimeOnly startsAt, TimeOnly endsAt)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await RescheduleSession.HandleAsync(
            sessionId, new RescheduleSession.Request(date, startsAt, endsAt), context, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private static WeeklySlot Slot(DayOfWeek day, int startHour, int endHour) =>
        new(day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    [Fact]
    public async Task Generate_OneWeeklySlot_CreatesOneSessionPerOccurrence()
    {
        var orgId = await SeedOrganizationAsync();
        // 2026-03-02 is a Monday.
        var courseId = await SeedCourseAsync(orgId, [Slot(DayOfWeek.Tuesday, 18, 19)], startsOn: new DateOnly(2026, 3, 2));

        var response = await GenerateAsync(orgId, courseId, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 29));

        // Four Tuesdays in that window: the 3rd, 10th, 17th and 24th.
        response.Generated.Should().Be(4);
        response.AlreadyScheduled.Should().Be(0);

        var sessions = await ListAsync(orgId, courseId);
        sessions.Should().HaveCount(4);
        sessions.Select(s => s.SessionDate).Should().Equal(
            new DateOnly(2026, 3, 3), new DateOnly(2026, 3, 10),
            new DateOnly(2026, 3, 17), new DateOnly(2026, 3, 24));
        sessions.Should().OnlyContain(s => s.Status == CourseSessionState.Scheduled);
    }

    [Fact]
    public async Task Generate_TwoSlotsOnTheSameDay_CreatesBothSessionsRatherThanCollidingOnTheDate()
    {
        // The exact risk GenerateSessions' own remarks call out: a morning
        // and an evening group sharing a weekday must not collide on the
        // (course_id, session_date) the old, wrong index would have
        // enforced.
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(
            orgId,
            [Slot(DayOfWeek.Saturday, 8, 9), Slot(DayOfWeek.Saturday, 18, 19)],
            startsOn: new DateOnly(2026, 3, 7)); // a Saturday

        var response = await GenerateAsync(orgId, courseId, new DateOnly(2026, 3, 7), new DateOnly(2026, 3, 7));

        response.Generated.Should().Be(2);

        var sessions = await ListAsync(orgId, courseId);
        sessions.Should().HaveCount(2);
        sessions.Select(s => s.StartsAt).Should().Equal(new TimeOnly(8, 0), new TimeOnly(18, 0));
    }

    [Fact]
    public async Task Generate_CalledTwiceOverTheSameRange_IsIdempotent()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, [Slot(DayOfWeek.Tuesday, 18, 19)], startsOn: new DateOnly(2026, 3, 2));

        var first = await GenerateAsync(orgId, courseId, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 15));
        var second = await GenerateAsync(orgId, courseId, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 15));

        first.Generated.Should().Be(2);
        second.Generated.Should().Be(0);
        second.AlreadyScheduled.Should().Be(2);

        (await ListAsync(orgId, courseId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Generate_RangeWiderThanTheCoursesOwnLife_ClampsToIt()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(
            orgId,
            [Slot(DayOfWeek.Tuesday, 18, 19)],
            startsOn: new DateOnly(2026, 3, 2),
            endsOn: new DateOnly(2026, 3, 9)); // the Monday before the second Tuesday

        var response = await GenerateAsync(orgId, courseId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        response.EffectiveFrom.Should().Be(new DateOnly(2026, 3, 2));
        response.EffectiveTo.Should().Be(new DateOnly(2026, 3, 9));
        // Only the 3rd of March falls inside that window.
        response.Generated.Should().Be(1);
    }

    [Fact]
    public async Task Generate_DeactivatedCourse_ReturnsConflict()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, [Slot(DayOfWeek.Tuesday, 18, 19)], isActive: false);

        var result = await GenerateRawAsync(orgId, courseId, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 8));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Generate_UnknownCourse_ReturnsNotFound()
    {
        var orgId = await SeedOrganizationAsync();

        var result = await GenerateRawAsync(orgId, Guid.NewGuid(), new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 8));

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task CancelThenReinstate_RoundTrips()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, [Slot(DayOfWeek.Tuesday, 18, 19)], startsOn: new DateOnly(2026, 3, 2));
        await GenerateAsync(orgId, courseId, new DateOnly(2026, 3, 3), new DateOnly(2026, 3, 3));
        var sessionId = (await ListAsync(orgId, courseId))[0].Id;

        Assert.IsType<NoContent>(await CancelAsync(orgId, sessionId, cancelled: true, reason: "Feriado"));

        var cancelled = (await ListAsync(orgId, courseId))[0];
        cancelled.Status.Should().Be(CourseSessionState.Cancelled);
        cancelled.CancellationReason.Should().Be("Feriado");

        Assert.IsType<NoContent>(await CancelAsync(orgId, sessionId, cancelled: false));

        var reinstated = (await ListAsync(orgId, courseId))[0];
        reinstated.Status.Should().Be(CourseSessionState.Scheduled);
        reinstated.CancellationReason.Should().BeNull();
    }

    [Fact]
    public void CancelValidator_CancellingWithNoReason_IsRejected()
    {
        var result = new CancelSession.Validator().Validate(new CancelSession.Request(true, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CancelValidator_ReinstatingNeedsNoReason()
    {
        var result = new CancelSession.Validator().Validate(new CancelSession.Request(false, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Reschedule_MovesDateAndTime()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, [Slot(DayOfWeek.Tuesday, 18, 19)], startsOn: new DateOnly(2026, 3, 2));
        await GenerateAsync(orgId, courseId, new DateOnly(2026, 3, 3), new DateOnly(2026, 3, 3));
        var sessionId = (await ListAsync(orgId, courseId))[0].Id;

        var result = await RescheduleAsync(
            orgId, sessionId, new DateOnly(2026, 3, 4), new TimeOnly(16, 0), new TimeOnly(17, 0));

        Assert.IsType<NoContent>(result);

        var moved = (await ListAsync(orgId, courseId))[0];
        moved.SessionDate.Should().Be(new DateOnly(2026, 3, 4));
        moved.StartsAt.Should().Be(new TimeOnly(16, 0));
        moved.EndsAt.Should().Be(new TimeOnly(17, 0));
    }

    [Fact]
    public async Task Reschedule_ACancelledSession_IsRefused()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, [Slot(DayOfWeek.Tuesday, 18, 19)], startsOn: new DateOnly(2026, 3, 2));
        await GenerateAsync(orgId, courseId, new DateOnly(2026, 3, 3), new DateOnly(2026, 3, 3));
        var sessionId = (await ListAsync(orgId, courseId))[0].Id;
        await CancelAsync(orgId, sessionId, cancelled: true);

        var result = await RescheduleAsync(
            orgId, sessionId, new DateOnly(2026, 3, 4), new TimeOnly(16, 0), new TimeOnly(17, 0));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Reschedule_OntoAnExistingSessionsDateAndTime_ReturnsConflict()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(
            orgId,
            [Slot(DayOfWeek.Tuesday, 18, 19), Slot(DayOfWeek.Thursday, 18, 19)],
            startsOn: new DateOnly(2026, 3, 2));
        await GenerateAsync(orgId, courseId, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 5));

        var sessions = await ListAsync(orgId, courseId);
        var tuesday = sessions.Single(s => s.SessionDate == new DateOnly(2026, 3, 3));
        var thursday = sessions.Single(s => s.SessionDate == new DateOnly(2026, 3, 5));

        var result = await RescheduleAsync(orgId, thursday.Id, tuesday.SessionDate, tuesday.StartsAt, tuesday.EndsAt);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ReadSessions_FromAndTo_NarrowTheCalendar()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, [Slot(DayOfWeek.Tuesday, 18, 19)], startsOn: new DateOnly(2026, 3, 2));
        await GenerateAsync(orgId, courseId, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 23));

        var narrowed = await ListAsync(orgId, courseId, from: new DateOnly(2026, 3, 10), to: new DateOnly(2026, 3, 17));

        narrowed.Select(s => s.SessionDate).Should().Equal(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 17));
    }
}

using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Courses;

/// <summary>
/// Lists the courses of the active organization, or reads one.
///
/// Neither query filters by organization: the isolation context already
/// restricts what the database will return, same reasoning as
/// <see cref="Clubs.ReadClubs"/>.
/// </summary>
public static class ReadCourses
{
    public sealed record Summary(
        Guid Id,
        string SportCode,
        string SportName,
        string Name,
        Guid? VenueSpaceId,
        string? VenueSpaceName,
        Guid? CoachMembershipId,
        string? CoachName,
        short? Capacity,
        decimal MonthlyFeeBob,
        DateOnly StartsOn,
        DateOnly? EndsOn,
        bool IsActive,
        IReadOnlyList<WeeklySlot> Schedules);

    public static IEndpointRouteBuilder MapReadCourses(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/courses", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadCourses))
            .WithSummary("Lists the courses of the active organization.");

        routes.MapGet("/courses/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadCourse")
            .WithSummary("Reads one course.");

        return routes;
    }

    internal static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        string? search = null,
        string? sportCode = null,
        bool activeOnly = false)
    {
        search = QueryFilter.OrAbsent(search);
        sportCode = QueryFilter.OrAbsent(sportCode);

        var courses = await Project(database.Courses
                .Where(course => search == null || EF.Functions.ILike(course.Name, $"%{search}%"))
                .Where(course => sportCode == null || course.SportCode == sportCode)
                .Where(course => !activeOnly || course.IsActive)
                .OrderBy(course => course.Name))
            .ToListAsync(cancellationToken);

        return Results.Ok(courses);
    }

    internal static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var course = await Project(database.Courses.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return course is null ? Results.NotFound() : Results.Ok(course);
    }

    private static IQueryable<Summary> Project(IQueryable<Course> courses) =>
        courses.Select(course => new Summary(
            course.Id,
            course.SportCode,
            course.Sport!.Name,
            course.Name,
            course.VenueSpaceId,
            course.VenueSpace == null ? null : course.VenueSpace.Name,
            course.CoachMembershipId,
            course.CoachMembership == null ? null : course.CoachMembership.User!.FullName,
            course.Capacity,
            course.MonthlyFeeBob,
            course.StartsOn,
            course.EndsOn,
            course.IsActive,
            course.Schedules
                .OrderBy(schedule => schedule.DayOfWeek)
                .ThenBy(schedule => schedule.StartsAt)
                .Select(schedule => new WeeklySlot(schedule.DayOfWeek, schedule.StartsAt, schedule.EndsAt))
                .ToList()));
}

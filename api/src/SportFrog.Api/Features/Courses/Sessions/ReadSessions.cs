using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Sessions;

/// <summary>Reads a course's calendar.</summary>
public static class ReadSessions
{
    public sealed record Summary(
        Guid Id,
        Guid CourseId,
        DateOnly SessionDate,
        TimeOnly StartsAt,
        TimeOnly EndsAt,
        CourseSessionState Status,
        string? CancellationReason);

    public static IEndpointRouteBuilder MapReadSessions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/courses/{courseId:guid}/sessions", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.Course, "courseId"))
            .WithName(nameof(ReadSessions))
            .WithSummary("Reads a course's calendar.");

        return routes;
    }

    /// <summary>
    /// The calendar, in date order. Everyone by default — a report on last
    /// month wants the sessions that already happened just as much as a
    /// view of what is coming up wants the ones that have not — narrowed to
    /// <paramref name="from"/>/<paramref name="to"/> only when given.
    /// </summary>
    internal static async Task<IResult> ListAsync(
        Guid courseId,
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        if (!await database.Courses.AnyAsync(course => course.Id == courseId, cancellationToken))
        {
            return Results.NotFound();
        }

        var sessions = await database.CourseSessions
            .Where(session => session.CourseId == courseId)
            .Where(session => from == null || session.SessionDate >= from)
            .Where(session => to == null || session.SessionDate <= to)
            .OrderBy(session => session.SessionDate)
            .ThenBy(session => session.StartsAt)
            .Select(session => new Summary(
                session.Id,
                session.CourseId,
                session.SessionDate,
                session.StartsAt,
                session.EndsAt,
                session.Status,
                session.CancellationReason))
            .ToListAsync(cancellationToken);

        return Results.Ok(sessions);
    }
}

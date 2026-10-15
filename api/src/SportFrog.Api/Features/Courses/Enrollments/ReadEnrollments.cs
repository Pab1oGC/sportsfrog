using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Enrollments;

/// <summary>
/// Reads the students enrolled in a course, or one enrollment.
/// </summary>
public static class ReadEnrollments
{
    /// <param name="WithdrawnAt">
    /// Set for someone who left the course. They stay in the list because
    /// they took it — a read of what happened in March has to include them —
    /// and a caller showing only who is currently enrolled filters on this,
    /// same reasoning as <see cref="Rosters.ReadRoster.Summary.WithdrawnAt"/>.
    /// </param>
    public sealed record Summary(
        Guid Id,
        Guid CourseId,
        Guid AthleteId,
        string FirstName,
        string LastName,
        string DocumentId,
        DateOnly BirthDate,
        DateTimeOffset EnrolledAt,
        DateTimeOffset? WithdrawnAt,
        decimal? FeeOverrideBob,
        string? FeeOverrideReason);

    public static IEndpointRouteBuilder MapReadEnrollments(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/courses/{courseId:guid}/enrollments", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.Course, "courseId"))
            .WithName(nameof(ReadEnrollments))
            .WithSummary("Reads the students enrolled in a course.");

        routes.MapGet("/enrollments/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.CourseEnrollment, "id"))
            .WithName("ReadEnrollment")
            .WithSummary("Reads one enrollment.");

        return routes;
    }

    /// <summary>
    /// The enrollments, enrolled students first and then those who left.
    /// </summary>
    /// <remarks>
    /// Everyone by default, same reasoning as <see cref="Rosters.ReadRoster"/>:
    /// a page listing today's students wants who is enrolled, while an
    /// attendance report for last month wants who was enrolled then.
    /// <c>active=true</c> narrows it; the unnarrowed answer is the complete
    /// one.
    /// </remarks>
    internal static async Task<IResult> ListAsync(
        Guid courseId,
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        bool active = false)
    {
        if (!await database.Courses.AnyAsync(course => course.Id == courseId, cancellationToken))
        {
            return Results.NotFound();
        }

        return Results.Ok(await Project(database.CourseEnrollments
                .Where(entry => entry.CourseId == courseId)
                .Where(entry => !active || entry.WithdrawnAt == null)
                .OrderBy(entry => entry.WithdrawnAt != null)
                .ThenBy(entry => entry.Athlete!.LastName)
                .ThenBy(entry => entry.Athlete!.FirstName))
            .ToListAsync(cancellationToken));
    }

    internal static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var entry = await Project(database.CourseEnrollments.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return entry is null ? Results.NotFound() : Results.Ok(entry);
    }

    private static IQueryable<Summary> Project(IQueryable<CourseEnrollment> entries) =>
        entries.Select(entry => new Summary(
            entry.Id,
            entry.CourseId,
            entry.AthleteId,
            entry.Athlete!.FirstName,
            entry.Athlete.LastName,
            entry.Athlete.DocumentId,
            entry.Athlete.BirthDate,
            entry.EnrolledAt,
            entry.WithdrawnAt,
            entry.FeeOverrideBob,
            entry.FeeOverrideReason));
}

using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Courses;

/// <summary>
/// Removes a course from the active organization.
///
/// Logically, always — same shape as <see cref="Clubs.DeleteClub"/>. No
/// usage guard yet: nothing can reference a course until students,
/// sessions and pension charges exist, and that is what the school
/// module's later migrations add.
/// </summary>
public static class DeleteCourse
{
    public static IEndpointRouteBuilder MapDeleteCourse(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/courses/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(DeleteCourse))
            .WithSummary("Removes a course.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var course = await database.Courses.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (course is null)
        {
            return Results.NotFound();
        }

        course.DeletedAt = clock.GetUtcNow();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

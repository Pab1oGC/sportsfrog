using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Courses;

/// <summary>Corrects a course, replacing its whole weekly schedule.</summary>
public static class UpdateCourse
{
    public static IEndpointRouteBuilder MapUpdateCourse(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/courses/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(UpdateCourse))
            .WithSummary("Corrects a course.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        CourseContract contract,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var course = await database.Courses
            .Include(candidate => candidate.Schedules)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (course is null)
        {
            return Results.NotFound();
        }

        if (!await database.Sports.AnyAsync(sport => sport.Code == contract.SportCode, cancellationToken))
        {
            return Results.Problem(
                detail: "No existe un deporte con ese código.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (contract.VenueSpaceId is { } venueSpaceId
            && !await database.VenueSpaces.AnyAsync(space => space.Id == venueSpaceId, cancellationToken))
        {
            return Results.Problem(
                detail: "Ningún espacio de esta organización tiene ese identificador.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (contract.CoachMembershipId is { } coachMembershipId
            && !await database.Memberships.AnyAsync(
                membership => membership.Id == coachMembershipId, cancellationToken))
        {
            return Results.Problem(
                detail: "Ningún miembro de esta organización tiene ese identificador.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var name = contract.Name.Trim();

        if (await database.Courses.AnyAsync(
                other => other.Id != id && other.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "Ya existe un curso con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        course.SportCode = contract.SportCode;
        course.Name = name;
        course.VenueSpaceId = contract.VenueSpaceId;
        course.CoachMembershipId = contract.CoachMembershipId;
        course.Capacity = contract.Capacity;
        course.MonthlyFeeBob = contract.MonthlyFeeBob;
        course.StartsOn = contract.StartsOn;
        course.EndsOn = contract.EndsOn;
        course.IsActive = contract.IsActive;

        // The whole set is replaced rather than diffed row by row — see
        // CourseContract's own remarks for why there is no finer-grained
        // endpoint to diff against in the first place. Clearing the loaded
        // collection is enough for the removal side: CourseId is required,
        // so EF has no way to keep an orphan and schedules a DELETE for
        // each one still in it.
        course.Schedules.Clear();

        // Added to the set directly, NOT through course.Schedules.Add().
        // CourseSchedule.Id is both store-generated (gen_random_uuid()) and
        // set here on the client — and when a new object with a non-default
        // key reaches the change tracker only by being discovered inside an
        // already-tracked entity's navigation collection, EF's fixup reads
        // that pre-set key as "this must already exist" and issues an
        // UPDATE instead of an INSERT, against an id no row has. Adding
        // straight to the DbSet marks it Added unambiguously, the same way
        // CreateCourse already adds the course itself.
        foreach (var slot in contract.Schedules)
        {
            database.CourseSchedules.Add(new CourseSchedule
            {
                Id = Guid.NewGuid(),
                OrgId = course.OrgId,
                CourseId = course.Id,
                DayOfWeek = slot.Day,
                StartsAt = slot.StartsAt,
                EndsAt = slot.EndsAt,
            });
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(
                detail: "Ya existe un curso con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}

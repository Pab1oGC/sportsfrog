using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses;

/// <summary>Registers a course the organization's sports school runs.</summary>
public static class CreateCourse
{
    public sealed record Response(Guid Id);

    public static IEndpointRouteBuilder MapCreateCourse(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/courses", HandleAsync)
            // Same level as a venue or a ruleset: set up once by whoever
            // administers the organization, not day-to-day operation.
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(CreateCourse))
            .WithSummary("Registers a course.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        CourseContract contract,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (!await database.Sports.AnyAsync(sport => sport.Code == contract.SportCode, cancellationToken))
        {
            return Results.Problem(
                detail: "No existe un deporte con ese código.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (contract.VenueSpaceId is { } venueSpaceId
            && !await database.VenueSpaces.AnyAsync(space => space.Id == venueSpaceId, cancellationToken))
        {
            // Not found rather than forbidden, same reasoning ReadClubs
            // documents for a club of another organization: RLS already
            // decided this row is not visible, so there is nothing here to
            // tell "doesn't exist" apart from "isn't yours".
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

        if (await database.Courses.AnyAsync(course => course.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "Ya existe un curso con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var course = new Course
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            SportCode = contract.SportCode,
            Name = name,
            VenueSpaceId = contract.VenueSpaceId,
            CoachMembershipId = contract.CoachMembershipId,
            Capacity = contract.Capacity,
            MonthlyFeeBob = contract.MonthlyFeeBob,
            StartsOn = contract.StartsOn,
            EndsOn = contract.EndsOn,
            IsActive = contract.IsActive,
        };

        database.Courses.Add(course);

        // Added to the set directly, not through course.Schedules — see
        // UpdateCourse's own remark on this exact line for why a navigation
        // Add() is the wrong call here.
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
            // Two requests naming the same course at once. The check above
            // settles the ordinary case and this settles the race, because
            // only the index sees both writes.
            return Results.Problem(
                detail: "Ya existe un curso con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/courses/{course.Id}", new Response(course.Id));
    }
}

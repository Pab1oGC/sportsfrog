using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Enrollments;

/// <summary>Registers a person to take a course.</summary>
public static class EnrollStudent
{
    public sealed record Request(Guid AthleteId);

    public sealed record Response(Guid Id);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.AthleteId)
                .NotEmpty().WithMessage("El estudiante es obligatorio.");
    }

    public static IEndpointRouteBuilder MapEnrollStudent(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/courses/{courseId:guid}/enrollments", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.Course, "courseId"))
            .WithName(nameof(EnrollStudent))
            .WithSummary("Registers a person to take a course.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid courseId,
        Request request,
        SportFrogDbContext database,
        EnrollmentPolicy policy,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (await CourseGate.OpenAsync(courseId, database, cancellationToken) is not { } opened)
        {
            return Results.NotFound();
        }

        if (opened.Refusal is { } refusal)
        {
            return refusal;
        }

        var course = opened.Course!;

        var athlete = await database.Athletes
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.AthleteId, cancellationToken);

        if (athlete is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AthleteId"] = ["Ningún estudiante de esta organización tiene ese identificador."],
            });
        }

        var violations = await policy.InspectAsync(course, athlete, excluding: null, cancellationToken);

        if (violations.Count > 0)
        {
            return Results.ValidationProblem(violations
                .GroupBy(violation => violation.Property)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(violation => violation.Message).ToArray()));
        }

        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CourseId = course.Id,
            AthleteId = athlete.Id,
        };

        database.CourseEnrollments.Add(enrollment);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two enrollments racing for the same person. The check above
            // answers the ordinary case; only the partial index sees this one.
            return Results.Problem(
                detail: "Esa inscripción choca con una hecha en el mismo instante. Volvé a leer " +
                        "la lista e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/enrollments/{enrollment.Id}", new Response(enrollment.Id));
    }
}

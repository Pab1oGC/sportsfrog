using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Enrollments;

/// <summary>
/// Registers several people to take a course in one step.
/// </summary>
/// <remarks>
/// <see cref="EnrollStudent"/> already does this one person at a time, and
/// still is how a late addition gets enrolled on its own — this exists for
/// the moment an operator picks several students from a roster at once,
/// the same shape <see cref="Rosters.RegisterPlayersBulk"/> already gives a
/// team.
///
/// All or nothing, the same promise that bulk makes: five people checked
/// one at a time against a course capacity of three would let the first
/// three in and refuse the last two for a reason that has nothing to do
/// with them — the course simply is not taking this many. Refusing all
/// five and naming exactly why, one message per name, is what actually
/// helps whoever is entering them.
/// </remarks>
public static class EnrollStudentsBulk
{
    public sealed record Request(IReadOnlyList<Guid> AthleteIds);

    public sealed record Response(IReadOnlyList<Guid> EnrollmentIds);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.AthleteIds)
                .NotEmpty().WithMessage("Hay que indicar al menos un estudiante.");

            RuleFor(request => request.AthleteIds)
                .Must(ids => ids.Distinct().Count() == ids.Count)
                .When(request => request.AthleteIds.Count > 0)
                .WithMessage("El mismo estudiante está repetido en la lista.");
        }
    }

    public static IEndpointRouteBuilder MapEnrollStudentsBulk(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/courses/{courseId:guid}/enrollments/bulk", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.Course, "courseId"))
            .WithName(nameof(EnrollStudentsBulk))
            .WithSummary("Registers several people to take a course in one step.");

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

        var athletesById = await database.Athletes
            .AsNoTracking()
            .Where(candidate => request.AthleteIds.Contains(candidate.Id))
            .ToDictionaryAsync(candidate => candidate.Id, cancellationToken);

        var missing = request.AthleteIds.Where(id => !athletesById.ContainsKey(id)).ToList();

        if (missing.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AthleteIds"] = [.. missing.Select(id =>
                    $"Ningún estudiante de esta organización tiene el identificador {id}.")],
            });
        }

        // Preserves the order the request named them in.
        var athletes = request.AthleteIds.Select(id => athletesById[id]).ToList();

        // `pending` is the position already reached in this same list, so
        // the second name in the batch sees the first one already counted
        // against the course's capacity and its own one-enrollment-per-
        // course rule, rather than each being checked as if arriving
        // alone — see EnrollmentPolicy.InspectAsync's own remarks.
        var violationsByAthlete = new Dictionary<string, string[]>();

        for (var index = 0; index < athletes.Count; index++)
        {
            var violations = await policy.InspectAsync(
                course, athletes[index], excluding: null, cancellationToken, pending: index);

            if (violations.Count > 0)
            {
                violationsByAthlete[$"AthleteIds[{index}]"] =
                    [.. violations.Select(violation => violation.Message)];
            }
        }

        if (violationsByAthlete.Count > 0)
        {
            return Results.ValidationProblem(violationsByAthlete);
        }

        var enrollments = athletes.Select(athlete => new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CourseId = course.Id,
            AthleteId = athlete.Id,
        }).ToList();

        database.CourseEnrollments.AddRange(enrollments);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // One of these people was enrolled elsewhere in the same
            // instant, racing this request. The checks above settle the
            // ordinary case; only the partial index sees this one. Nothing
            // in this batch was saved — one failed insert rolls the whole
            // batch back, the same "all or nothing" the checks above
            // already promised.
            return Results.Problem(
                detail: "Una de estas inscripciones choca con una hecha en el mismo instante. " +
                        "Volvé a leer la lista e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created(
            $"/courses/{course.Id}/enrollments",
            new Response([.. enrollments.Select(enrollment => enrollment.Id)]));
    }
}

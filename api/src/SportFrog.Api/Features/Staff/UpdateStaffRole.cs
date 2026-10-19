using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Staff;

/// <summary>
/// Corrects the role a staff member was registered under.
/// </summary>
/// <remarks>
/// Only the role, deliberately. Who the person is cannot be corrected here:
/// registering the wrong person is not a role entered wrongly, it is a row
/// that should never have been written, and <see cref="StrikeStaff"/> is the
/// half that has to be argued for. The same split
/// <c>Features.Rosters.CorrectRegistration</c> and
/// <c>StrikeRegistration</c> already make.
/// </remarks>
public static class UpdateStaffRole
{
    public sealed record Request(StaffRole Role);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.Role)
                .IsInEnum().WithMessage("El cargo no es válido.");
    }

    public static IEndpointRouteBuilder MapUpdateStaffRole(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/staff/{id:guid}/role", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Club, ScopeTargetKind.TeamStaff, "id"))
            .WithName(nameof(UpdateStaffRole))
            .WithSummary("Corrects the role a staff member was registered under.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        StaffPolicy policy,
        CancellationToken cancellationToken)
    {
        var staff = await database.TeamStaff.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (staff is null)
        {
            return Results.NotFound();
        }

        if (staff.Role == request.Role)
        {
            // Already what the caller is asking for. Answered as success: a
            // second save of an unchanged form is not a mistake to report.
            return Results.NoContent();
        }

        var violations = await policy.InspectAsync(
            staff.TeamId, staff.AthleteId, request.Role, excluding: id, cancellationToken);

        if (violations.Count > 0)
        {
            return StaffPolicy.Refuse(violations);
        }

        staff.Role = request.Role;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Somebody else took the head coach's seat in the same instant.
            return Results.Problem(
                detail: "Ese cargo se ocupó en el mismo instante. Volvé a leer el cuerpo técnico " +
                        "e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}

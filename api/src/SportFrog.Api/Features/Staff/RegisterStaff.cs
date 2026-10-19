using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Features.Rosters;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Staff;

/// <summary>
/// Registers a person onto a team's staff: the bench, or the board.
/// </summary>
/// <remarks>
/// The person comes from the organization's own people — the same rows a
/// player is picked from, created through the same endpoint and the same form,
/// which is what "registered the same way as an athlete" means here. What
/// differs is only this step: no shirt, no eligibility to play, and a role
/// instead of a position.
/// </remarks>
public static class RegisterStaff
{
    public sealed record Request(Guid AthleteId, StaffRole Role);

    public sealed record Response(Guid Id);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.AthleteId)
                .NotEmpty().WithMessage("La persona es obligatoria.");

            RuleFor(request => request.Role)
                .IsInEnum().WithMessage("El cargo no es válido.");
        }
    }

    public static IEndpointRouteBuilder MapRegisterStaff(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/teams/{teamId:guid}/staff", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Club, ScopeTargetKind.Team, "teamId"))
            .WithName(nameof(RegisterStaff))
            .WithSummary("Registers a person onto a team's technical staff or board.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid teamId,
        Request request,
        SportFrogDbContext database,
        StaffPolicy policy,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        // The same gate a player passes through, and for the same three
        // reasons: no such team, a competition already over, a team that
        // withdrew from its category. A bench is not registrable where a
        // squad is not.
        if (await RosterTeamGate.OpenAsync(teamId, database, cancellationToken) is not { } opened)
        {
            return Results.NotFound();
        }

        if (opened.Refusal is { } refusal)
        {
            return refusal;
        }

        var team = opened.Team!;

        if (!await database.Athletes
                .AsNoTracking()
                .AnyAsync(candidate => candidate.Id == request.AthleteId, cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AthleteId"] = ["Ninguna persona de esta organización tiene ese identificador."],
            });
        }

        var violations = await policy.InspectAsync(
            team.Id, request.AthleteId, request.Role, excluding: null, cancellationToken);

        if (violations.Count > 0)
        {
            return StaffPolicy.Refuse(violations);
        }

        var staff = new TeamStaff
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            TeamId = team.Id,
            AthleteId = request.AthleteId,
            Role = request.Role,
        };

        database.TeamStaff.Add(staff);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two registrations racing: the same person twice, or two people
            // claiming the one head coach's seat. The checks above answer the
            // ordinary case; only the indexes see this one.
            return Results.Problem(
                detail: "Ese registro choca con uno hecho en el mismo instante. Volvé a leer el " +
                        "cuerpo técnico e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/staff/{staff.Id}", new Response(staff.Id));
    }
}

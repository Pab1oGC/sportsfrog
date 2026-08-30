using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Rosters;

/// <summary>
/// Registers a person to play for a team.
/// </summary>
public static class RegisterPlayer
{
    public sealed record Request(Guid AthleteId, short? JerseyNumber, string? Position);

    public sealed record Response(Guid Id);

    internal sealed class Validator : AbstractValidator<Request>
    {
        /// <summary>
        /// Three digits is past every shirt anyone wears and short of a typed
        /// year. The bound catches a slip, not a rule.
        /// </summary>
        private const short MaximumJerseyNumber = 999;

        public Validator()
        {
            RuleFor(request => request.AthleteId)
                .NotEmpty().WithMessage("El deportista es obligatorio.");

            RuleFor(request => request.JerseyNumber)
                .InclusiveBetween((short)0, MaximumJerseyNumber)
                .When(request => request.JerseyNumber.HasValue)
                .WithMessage($"El número de camiseta está entre 0 y {MaximumJerseyNumber}, o se " +
                             "deja sin definir hasta que se repartan los números.");

            RuleFor(request => request.Position)
                .MaximumLength(40)
                .When(request => request.Position is not null);
        }
    }

    public static IEndpointRouteBuilder MapRegisterPlayer(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/teams/{teamId:guid}/roster", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(RegisterPlayer))
            .WithSummary("Registers a person to play for a team.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid teamId,
        Request request,
        SportFrogDbContext database,
        RosterPolicy policy,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var team = await database.Teams
            .AsNoTracking()
            .Include(candidate => candidate.Category)
                .ThenInclude(category => category!.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == teamId, cancellationToken);

        if (team?.Category is not { Competition: { } competition } category)
        {
            return Results.NotFound();
        }

        if (competition.Status is CompetitionState.Finished or CompetitionState.Cancelled)
        {
            // Registering into a competition that is over does not add a
            // player to anything; it edits history. Mid-season is left open on
            // purpose — squads change while a league runs, and refusing that
            // would be refusing how the sport works.
            return Results.Problem(
                detail: "Esta competencia ya terminó, así que sus nóminas están cerradas.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (!team.IsActive)
        {
            return Results.Problem(
                detail: $"{team.Name} se retiró de esta categoría, así que no está tomando " +
                        "registros.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var athlete = await database.Athletes
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.AthleteId, cancellationToken);

        if (athlete is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AthleteId"] = ["Ningún deportista de esta organización tiene ese identificador."],
            });
        }

        var violations = await policy.InspectAsync(
            team, category, athlete, request.JerseyNumber, excluding: null, cancellationToken);

        if (violations.Count > 0)
        {
            // Every reason at once. Someone filling in a squad sheet should
            // learn that the player is too old *and* that the shirt is taken
            // in one answer, not across two attempts.
            return Results.ValidationProblem(violations
                .GroupBy(violation => violation.Property)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(violation => violation.Message).ToArray()));
        }

        var entry = new RosterEntry
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            TeamId = team.Id,
            AthleteId = athlete.Id,
            JerseyNumber = request.JerseyNumber,
            Position = string.IsNullOrWhiteSpace(request.Position) ? null : request.Position.Trim(),
        };

        database.RosterEntries.Add(entry);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two registrations racing: the same person twice, or two players
            // claiming one shirt. The checks above answer the ordinary case;
            // only the indexes see this one.
            return Results.Problem(
                detail: "Ese registro choca con uno hecho en el mismo instante. Volvé a leer la " +
                        "nómina e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/roster/{entry.Id}", new Response(entry.Id));
    }
}

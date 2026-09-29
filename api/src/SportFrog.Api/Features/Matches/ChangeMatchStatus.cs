using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Starts a match, calls it off, or puts it back on the calendar.
/// </summary>
/// <remarks>
/// The states a fixture reaches without a score. Finishing and awarding are
/// not here: both carry something the state alone cannot express — a score,
/// a winner — and the schema refuses either without it.
/// </remarks>
public static class ChangeMatchStatus
{
    public sealed record Request(string Status);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Status)
                .Must(status => WireEnum.TryParse<MatchState>(status, out _))
                .WithMessage(
                    $"Estado desconocido. Disponibles: {WireEnum.Options<MatchState>()}.");
        }
    }

    public static IEndpointRouteBuilder MapChangeMatchStatus(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/matches/{id:guid}/status", HandleAsync)
            // Postponing and calling a match off reshape the calendar, which
            // is the operator's work rather than the recorder's.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(ChangeMatchStatus))
            .WithSummary("Starts, postpones, cancels or reschedules a match.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var match = await database.Matches.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (match is null)
        {
            return Results.NotFound();
        }

        var target = WireEnum.Parse<MatchState>(request.Status);

        if (target == match.Status)
        {
            // Already there. A second tap on "start" is not a mistake to
            // report.
            return Results.NoContent();
        }

        if (target is MatchState.Finished or MatchState.Walkover)
        {
            return Results.Problem(
                detail: target == MatchState.Finished
                    ? "Un partido termina registrando su resultado, no nombrando el estado: " +
                      "usá el endpoint de resultado."
                    : "Un walkover se otorga a un equipo, así que necesita nombrar uno: usá el " +
                      "endpoint de walkover.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (target is MatchState.InProgress && !match.HasBothTeams)
        {
            // A knockout drawn in full names this fixture's round and phase
            // before it names its teams — starting it before then would put
            // a match "in progress" between two sides nobody can name yet.
            return Results.Problem(
                detail: "Este partido todavía no tiene los dos equipos definidos: espera a que " +
                        "termine el partido anterior de la llave.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (!MatchLifecycle.CanMove(match.Status, target))
        {
            var destinations = MatchLifecycle.Destinations(match.Status);

            return Results.Problem(
                detail: destinations.Count == 0
                    ? $"Este partido está {WireEnum.Label(match.Status)}, y eso lo decide su " +
                      "resultado. Corregí el resultado en su lugar."
                    : $"Un partido {WireEnum.Label(match.Status)} solo puede pasar a: " +
                      $"{string.Join(", ", destinations.Select(WireEnum.Label))}.",
                statusCode: StatusCodes.Status409Conflict);
        }

        match.Status = target;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // uq_space_schedule counts only live fixtures, so a postponed or
            // cancelled match frees its slot — and putting one back onto the
            // calendar can find the slot taken in the meantime.
            return Results.Problem(
                detail: "Otro partido tomó ese espacio y horario mientras este estaba fuera del " +
                        "calendario. Moverlo antes de ponerlo de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody else changed this same match's status, or its result,
            // in the meantime.
            return Results.Problem(
                detail: "Alguien más cambió este partido mientras vos lo tenías abierto. Volvé a " +
                        "leerlo antes de cambiar su estado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}

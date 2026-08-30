using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Moves a competition through its life: set up, announced, under way,
/// closed.
/// </summary>
/// <remarks>
/// A separate operation from editing, and that separation is the design. An
/// edit that could also set the status would let a competition arrive at
/// "finished" without ever passing through being played, and the whole value
/// of the state is that it was reached by a route somebody had to take.
/// </remarks>
public static class ChangeCompetitionStatus
{
    public sealed record Request(string Status);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Status)
                .Must(status => WireEnum.TryParse<CompetitionState>(status, out _))
                .WithMessage(
                    $"Estado desconocido. Disponibles: {WireEnum.Options<CompetitionState>()}.");
        }
    }

    public static IEndpointRouteBuilder MapChangeCompetitionStatus(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/competitions/{id:guid}/status", HandleAsync)
            // Running a competition is the operator's job: announcing the
            // fixtures, starting the event, closing it when the last match is
            // played. Cancelling is not part of running one, and is checked
            // separately below.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(ChangeCompetitionStatus))
            .WithSummary("Moves a competition to another state.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        CompetitionActivity activity,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (competition is null)
        {
            return Results.NotFound();
        }

        var target = WireEnum.Parse<CompetitionState>(request.Status);

        if (target == competition.Status)
        {
            // Already there. Answered as success because the caller asked for
            // a state and the competition is in it — a second click on
            // "start" should not read as an error.
            return Results.NoContent();
        }

        if (!CompetitionLifecycle.CanMove(competition.Status, target))
        {
            var destinations = CompetitionLifecycle.Destinations(competition.Status);

            return Results.Problem(
                detail: destinations.Count == 0
                    ? $"Una competencia {WireEnum.Label(competition.Status)} se queda así. " +
                      "Armá una nueva en su lugar."
                    : $"Una competencia {WireEnum.Label(competition.Status)} solo puede pasar " +
                      $"a: {string.Join(", ", destinations.Select(WireEnum.Label))}.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (target == CompetitionState.Cancelled
            && organization.Role?.Reaches(MembershipRole.Admin) is not true)
        {
            // Abandoning the event is not part of running it. Nothing about
            // which role would have been enough, for the same reason the role
            // filter says nothing.
            return Results.Problem(
                detail: "Esta operación no está permitida.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (await RefuseAsync(competition, target, database, activity, cancellationToken)
            is { } refusal)
        {
            return refusal;
        }

        competition.Status = target;

        // A competition that goes back to being set up stops being public.
        // Its fixtures are about to change, and leaving the old ones on a
        // page anybody can read is worse than showing nothing.
        if (target == CompetitionState.Draft)
        {
            competition.IsPublic = false;
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// The checks that depend on what has been played rather than on the
    /// shape of the move.
    /// </summary>
    private static async Task<IResult?> RefuseAsync(
        Competition competition,
        CompetitionState target,
        SportFrogDbContext database,
        CompetitionActivity activity,
        CancellationToken cancellationToken)
    {
        switch (competition.Status, target)
        {
            case (CompetitionState.Draft, CompetitionState.Scheduled)
                when !await database.Categories.AnyAsync(
                    category => category.CompetitionId == competition.Id, cancellationToken):
                // Nothing to draw a calendar for. A competition with no
                // divisions has no teams either, so announcing it would
                // publish an empty page.
                return Results.Problem(
                    detail: "Esta competencia todavía no tiene categorías, así que no hay nada " +
                            "que programar. Agregá al menos una división primero.",
                    statusCode: StatusCodes.Status409Conflict);

            case (CompetitionState.Scheduled, CompetitionState.Draft)
                when await activity.HasResultsAsync(competition.Id, cancellationToken):
                // Going back to setup means the fixtures are about to be
                // redrawn, and a fixture that already produced a result
                // cannot be redrawn without deciding what happens to the
                // result. Nothing here is prepared to make that decision.
                return Results.Problem(
                    detail: "Ya se registraron resultados, así que esta competencia no puede " +
                            "volver a estar en armado. Cancelala en su lugar si se está " +
                            "abandonando.",
                    statusCode: StatusCodes.Status409Conflict);

            case (CompetitionState.InProgress, CompetitionState.Finished)
                when await activity.HasMatchesUnderWayAsync(competition.Id, cancellationToken):
                return Results.Problem(
                    detail: "Todavía hay un partido en curso. Cerralo antes de cerrar la " +
                            "competencia.",
                    statusCode: StatusCodes.Status409Conflict);

            default:
                return null;
        }
    }
}

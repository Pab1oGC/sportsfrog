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
                    $"Unknown state. Available: {WireEnum.Options<MatchState>()}.");
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
                    ? "A match is finished by recording its result, not by naming the state: " +
                      "use the result endpoint."
                    : "A walkover is awarded to a team, so it needs to name one: use the " +
                      "walkover endpoint.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (!MatchLifecycle.CanMove(match.Status, target))
        {
            var destinations = MatchLifecycle.Destinations(match.Status);

            return Results.Problem(
                detail: destinations.Count == 0
                    ? $"This match is {WireEnum.Label(match.Status)}, and that is decided by its " +
                      "result. Correct the result instead."
                    : $"A {WireEnum.Label(match.Status)} match can only move to: " +
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
                detail: "Another match has taken that space and time while this one was off the " +
                        "calendar. Move it before putting it back.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}

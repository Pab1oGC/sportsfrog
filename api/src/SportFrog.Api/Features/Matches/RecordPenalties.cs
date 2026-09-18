using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Breaks the tie of a knockout match that ended level, by shootout.
/// </summary>
/// <remarks>
/// A league match is allowed to end level — a draw is a real result, worth
/// whatever the ruleset prices it at. A knockout match is not: <see
/// cref="Draw.AdvanceBracket"/> refuses to draw a next round out of a tie it
/// cannot resolve into a winner, and this is how one gets resolved.
///
/// Kept apart from <see cref="RecordResult"/> on purpose. The shootout is not
/// part of what happened in the 90 minutes — the match stays a draw on the
/// scoreline forever, and only the bracket needs to know who went through.
/// </remarks>
public static class RecordPenalties
{
    public sealed record Request(short HomeScore, short AwayScore);

    public sealed record Response(Guid WinnerTeamId);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.HomeScore).GreaterThanOrEqualTo((short)0);
            RuleFor(request => request.AwayScore).GreaterThanOrEqualTo((short)0);

            RuleFor(request => request)
                .Must(request => request.HomeScore != request.AwayScore)
                .WithMessage("Un desempate por penales que también termina igualado no decidió nada.");
        }
    }

    public static IEndpointRouteBuilder MapRecordPenalties(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/matches/{id:guid}/penalties", HandleAsync)
            .RequireRole(MembershipRole.Recorder)
            .WithName(nameof(RecordPenalties))
            .WithSummary("Records the shootout that broke a level knockout match.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var match = await database.Matches.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (match is null)
        {
            return Results.NotFound();
        }

        if (match.Status is not MatchState.Finished)
        {
            return Results.Problem(
                detail: "Un desempate por penales corrige a quién avanza un partido ya jugado. " +
                        "Registrá el resultado primero.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (match.HomeTotal != match.AwayTotal)
        {
            return Results.Problem(
                detail: "Este partido no terminó igualado, así que no hay nada que desempatar por penales.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (match.Phase is null)
        {
            // Phase is null for exactly the matches AdvanceBracket does not
            // ask a winner of — the group stage, and any format that is not a
            // knockout at all. A shootout only exists to answer a question
            // this match was never going to be asked.
            return Results.Problem(
                detail: "Los penales desempatan una eliminatoria. Este partido es de fase de " +
                        "grupos, donde un empate ya es un resultado válido.",
                statusCode: StatusCodes.Status409Conflict);
        }

        match.PenaltyHomeScore = request.HomeScore;
        match.PenaltyAwayScore = request.AwayScore;
        match.ModifiedBy = organization.UserId;
        match.ModifiedAt = clock.GetUtcNow();

        await BracketWinnerPropagation.ApplyAsync(match, database, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        // A match reaches Finished only through RecordResult, which already
        // refuses one without both teams named.
        var winnerTeamId = request.HomeScore > request.AwayScore ? match.HomeTeamId!.Value : match.AwayTeamId!.Value;

        return Results.Ok(new Response(winnerTeamId));
    }
}

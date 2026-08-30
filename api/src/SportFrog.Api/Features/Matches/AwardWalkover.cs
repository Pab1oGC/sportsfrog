using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Awards a match to the side that turned up.
/// </summary>
/// <remarks>
/// A result without a match. Nobody played it, but it stands in the table
/// exactly like one that was played, which is why it is a state of its own
/// rather than a cancellation: cancelling would remove it from the table and
/// let the absent side off.
///
/// The score comes from the ruleset, not from the request. What a walkover is
/// worth was decided when the competition was set up, and typing it per match
/// would make two of them in one league worth different things.
/// </remarks>
public static class AwardWalkover
{
    public sealed record Request(Guid WinnerTeamId, string? Notes);

    public sealed record Response(int HomeTotal, int AwayTotal);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.WinnerTeamId)
                .NotEmpty().WithMessage("El equipo al que se otorga el partido es obligatorio.");

            RuleFor(request => request.Notes)
                .MaximumLength(1000)
                .When(request => request.Notes is not null);
        }
    }

    public static IEndpointRouteBuilder MapAwardWalkover(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/matches/{id:guid}/walkover", HandleAsync)
            // Awarding a match nobody played is a ruling, not a recording.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(AwardWalkover))
            .WithSummary("Awards a match to the team that appeared.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        ResultPolicy policy,
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

        if (match.Status is MatchState.Finished)
        {
            return Results.Problem(
                detail: "Este partido se jugó y tiene un resultado, así que no se puede otorgar. " +
                        "Corregí el resultado en su lugar.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (request.WinnerTeamId != match.HomeTeamId && request.WinnerTeamId != match.AwayTeamId)
        {
            // The award has to go to one of the two sides, and naming a third
            // is a request built against the wrong fixture.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["WinnerTeamId"] = ["Ese equipo no juega este partido."],
            });
        }

        if (await policy.FindRulesAsync(match, cancellationToken) is not { } rules)
        {
            return Results.Problem(
                detail: "No se pueden leer las reglas de este partido, así que no se puede anotar un walkover.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (ResultPolicy.WalkoverScore(rules) is not { } award)
        {
            // The ruleset says nothing about walkovers, so there is no score
            // to award. Refused rather than invented: a number chosen here
            // would quietly become the league's rule.
            return Results.Problem(
                detail: "El reglamento de esta competencia no dice cuánto vale un walkover. " +
                        "Agregalo al reglamento antes de otorgar uno.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var homeWon = request.WinnerTeamId == match.HomeTeamId;

        match.WalkoverTeamId = request.WinnerTeamId;
        match.Status = MatchState.Walkover;
        match.HomeTotal = homeWon ? award.Winner : award.Loser;
        match.AwayTotal = homeWon ? award.Loser : award.Winner;

        // Nobody played, so there are no periods. Cleared rather than left
        // alone, because a match being awarded after starting would otherwise
        // keep the half it did play.
        match.PeriodScores = null;

        if (request.Notes is not null)
        {
            match.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        }

        var now = clock.GetUtcNow();

        if (match.RecordedBy is null)
        {
            match.RecordedBy = organization.UserId;
            match.RecordedAt = now;
        }
        else
        {
            match.ModifiedBy = organization.UserId;
            match.ModifiedAt = now;
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(match.HomeTotal.Value, match.AwayTotal.Value));
    }
}

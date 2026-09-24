using Microsoft.EntityFrameworkCore;
using FluentValidation;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Features.Standings;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Domain.Scheduling;
using SportFrog.Domain.Standings;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Draws the knockout stage of a category that played a group stage first.
/// </summary>
/// <remarks>
/// The other half of the format <see cref="CompetitionFormat.Groups"/>
/// promises in its own name and never used to deliver: everything up to now
/// only drew the round-robin inside each group. This is what turns the
/// standings that produced into a bracket, and it is deliberately a second
/// operation rather than a flag on <see cref="DrawCalendar"/> — the group
/// stage has to be finished and read before anyone can be told who plays
/// next, in a way the opening draw never has to wait for.
///
/// Draws the whole bracket, quarter-finals to final, the way a pure knockout
/// does: only round one has teams in it, and every later match is created
/// with its sides waiting on the winners of the ones before it.
///
/// How many advance is asked here rather than fixed on the competition,
/// because the answer is a decision the organizer makes once the tables are
/// final — two per group is common, but not universal, and "best thirds" is
/// exactly that: a rule about groups that finished, not one a format can
/// declare in advance.
/// </remarks>
public static class PromoteGroupStage
{
    /// <param name="QualifiersPerGroup">
    /// How many finish high enough in their own group to qualify outright.
    /// </param>
    /// <param name="BestThirdPlaced">
    /// How many more qualify by out-ranking the other groups' next-best —
    /// the classic "mejores terceros" when two per group already qualify
    /// directly, generalized to whatever rank the cutoff actually leaves.
    /// </param>
    public sealed record Request(int QualifiersPerGroup, int BestThirdPlaced);

    /// <param name="Created">Every match of the bracket, first round to final.</param>
    /// <param name="Phase">The first round's name: the one about to be played.</param>
    /// <param name="RepeatedMatchups">
    /// How many of round one's pairings repeat a group-stage meeting —
    /// normally zero, and only ever positive when the numbers themselves
    /// leave no other way to pair everyone.
    /// </param>
    /// <param name="Rounds">How many rounds the bracket has, first to final.</param>
    public sealed record Response(
        int Created,
        int Replaced,
        string? Phase,
        int Direct,
        int Wildcards,
        int Byes,
        int RepeatedMatchups,
        int Rounds = 0);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.QualifiersPerGroup)
                .InclusiveBetween(1, 8)
                .WithMessage("Por grupo clasifican entre 1 y 8 equipos.");

            RuleFor(request => request.BestThirdPlaced)
                .InclusiveBetween(0, 16)
                .WithMessage("Los cupos por mejor ubicado están entre 0 y 16.");
        }
    }

    public static IEndpointRouteBuilder MapPromoteGroupStage(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/draw/knockout", HandleAsync)
            // Same operator's work as drawing the calendar in the first place.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(PromoteGroupStage))
            .WithSummary("Draws the knockout stage from a finished group stage.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories
            .Include(candidate => candidate.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category?.Competition is not { } competition)
        {
            return Results.NotFound();
        }

        if (competition.Format != CompetitionFormat.Groups)
        {
            return Results.Problem(
                detail: $"Esta operación arma la eliminatoria de una fase de grupos. Esta " +
                        $"competencia está sorteada como {competition.Format}.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (competition.Status is CompetitionState.Finished or CompetitionState.Cancelled)
        {
            return Results.Problem(
                detail: "Esta competencia ya terminó, así que su calendario está cerrado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var existing = await database.Matches
            .Where(match => match.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        // Phase is what tells the two stages apart on the same category: the
        // group draw never sets it, and every knockout match, including this
        // one's, always does.
        var groupMatches = existing.Where(match => match.Phase is null).ToList();
        var knockoutMatches = existing.Where(match => match.Phase is not null).ToList();

        if (groupMatches.Count == 0)
        {
            return Results.Problem(
                detail: "Esta categoría todavía no tiene un calendario de grupos sorteado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (groupMatches.Any(match => match.Status is not (MatchState.Finished or MatchState.Walkover)))
        {
            return Results.Problem(
                detail: "No todos los partidos de la fase de grupos tienen resultado, así que " +
                        "todavía no se sabe quién clasifica.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (knockoutMatches.Any(match => match.Status is MatchState.Finished or MatchState.Walkover))
        {
            // The same rule DrawCalendar applies to a redraw: once a result
            // is attached to a fixture, replacing the fixture would orphan
            // the result rather than correct anything.
            return Results.Problem(
                detail: "La fase eliminatoria de esta categoría ya tiene resultados cargados, así " +
                        "que no se puede volver a sortear.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var standings = await StandingsQuery.ForCategoryAsync(database, categoryId, cancellationToken);

        if (standings is null)
        {
            return Results.NotFound();
        }

        var (plan, problem) = GroupStageAdvancement.Build(
            standings.Groups, request.QualifiersPerGroup, request.BestThirdPlaced);

        if (plan is null)
        {
            return Results.Problem(detail: problem, statusCode: StatusCodes.Status409Conflict);
        }

        // Every round down to the final, drawn now: round one from the
        // qualifiers, and each later round from slots that say "whoever wins
        // that match" — see Bracket.FullDraw. The same shape a pure knockout
        // has, so dates and venues for the whole bracket can be booked before
        // it is played, and BracketWinnerPropagation fills each slot in as its
        // match is decided. Computed before anything is replaced: a plan with
        // nothing in it must not cost the organizer the bracket they had.
        var bracket = Bracket.FullDraw(plan.Seeded);

        if (bracket.Count == 0)
        {
            return Results.Problem(
                detail: "Hacen falta al menos dos clasificados para armar una eliminatoria.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var now = clock.GetUtcNow();

        // A promotion is ordinary to redraw too — an organizer who picked
        // three per group and meant two has not broken anything yet, as
        // long as nobody has played.
        foreach (var match in knockoutMatches)
        {
            match.DeletedAt = now;
        }

        database.Matches.AddRange(PlannedBracket.ToMatches(
            bracket, organization.RequireOrganizationId(), competition.Id, categoryId));

        // Still recorded, though the bracket no longer needs it to tell a bye
        // from an eliminated team: AdvanceBracket reads it for a bracket
        // promoted before this drew every round, which has to keep working.
        category.KnockoutEntrants = [.. plan.Seeded];

        await database.SaveChangesAsync(cancellationToken);

        var firstRound = bracket.Count(match => match.Round == 1);

        return Results.Ok(new Response(
            bracket.Count,
            knockoutMatches.Count,
            bracket[0].Phase,
            plan.Direct,
            plan.Wildcards,
            plan.Seeded.Count - firstRound * 2,
            plan.RepeatedMatchups,
            bracket.Max(match => match.Round)));
    }
}

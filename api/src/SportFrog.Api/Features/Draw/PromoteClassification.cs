using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Features.Performances;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Draws the knockout stage of a category that ran a classification stage
/// first.
/// </summary>
/// <remarks>
/// The performance counterpart to <see cref="PromoteGroupStage"/>, and for
/// the same reason a second operation rather than a flag on
/// <see cref="DrawCalendar"/>: the classification stage has to be finished
/// and read before anyone can be told who plays next.
///
/// Considerably simpler than its counterpart, because a classification stage
/// never had groups to keep apart in the first place — see
/// <see cref="SportFrog.Domain.Performances.ClassificationAdvancement"/> for
/// why there is nothing here to reseed against a rematch.
/// </remarks>
public static class PromoteClassification
{
    /// <param name="Qualifiers">
    /// How many advance out of the classification stage. A tie at the
    /// cutoff is never split, so the field a bracket actually gets can come
    /// out a little larger than this.
    /// </param>
    public sealed record Request(int Qualifiers);

    public sealed record Response(int Created, int Replaced, string? Phase, int Qualified, int Byes);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.Qualifiers)
                .GreaterThanOrEqualTo(2)
                .WithMessage("Clasifican al menos dos competidores.");
    }

    public static IEndpointRouteBuilder MapPromoteClassification(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/performances/promote", HandleAsync)
            // Same operator's work as drawing the calendar in the first place.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(PromoteClassification))
            .WithSummary("Draws the knockout stage from a finished classification stage.");

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
                .ThenInclude(competition => competition!.Sport)
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category?.Competition is not { Sport: { } sport } competition)
        {
            return Results.NotFound();
        }

        if (sport.ScoreMode != ScoreMode.Judged)
        {
            return Results.Problem(
                detail: $"{sport.Name} no se decide por puntaje de jueces, así que no tiene una " +
                        "etapa de clasificación que promover.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (competition.Format != CompetitionFormat.Knockout)
        {
            return Results.Problem(
                detail: $"Esta operación arma la eliminatoria de una etapa de clasificación. Esta " +
                        $"competencia está sorteada como {competition.Format}.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (competition.Status is CompetitionState.Finished or CompetitionState.Cancelled)
        {
            return Results.Problem(
                detail: "Esta competencia ya terminó, así que su calendario está cerrado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var performances = await PerformancesQuery.ForCategoryAsync(database, categoryId, cancellationToken);

        var existing = await database.Matches
            .Where(match => match.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        if (existing.Any(match => match.Status is MatchState.Finished or MatchState.Walkover))
        {
            // Same rule DrawCalendar and PromoteGroupStage apply to a
            // redraw: once a result is attached to a fixture, replacing the
            // fixture would orphan the result rather than correct anything.
            return Results.Problem(
                detail: "La fase eliminatoria de esta categoría ya tiene resultados cargados, así " +
                        "que no se puede volver a sortear.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var (plan, problem) = ClassificationAdvancement.Build(performances, request.Qualifiers);

        if (plan is null)
        {
            return Results.Problem(detail: problem, statusCode: StatusCodes.Status409Conflict);
        }

        var now = clock.GetUtcNow();

        // A promotion is ordinary to redraw too, same as PromoteGroupStage —
        // an organizer who asked for four qualifiers and meant eight has not
        // broken anything yet, as long as nobody has played.
        foreach (var match in existing)
        {
            match.DeletedAt = now;
        }

        var (drawn, byes) = Bracket.FirstRound(plan.Seeded);
        var phase = Bracket.Phase(drawn.Count, 1);

        database.Matches.AddRange(drawn.Select(fixture => new Match
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CompetitionId = competition.Id,
            CategoryId = categoryId,
            HomeTeamId = fixture.HomeTeamId,
            AwayTeamId = fixture.AwayTeamId,
            RoundNumber = 1,
            Phase = phase,
            Status = MatchState.Scheduled,
        }));

        // Read back by AdvanceBracket once the bracket is finished, so a
        // later round can tell a genuine bye from a competitor the
        // classification stage ranked out — both are active teams that
        // never play a knockout match, and only this says which is which.
        category.KnockoutEntrants = [.. plan.Seeded];

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(drawn.Count, existing.Count, phase, plan.Seeded.Count, byes.Count));
    }
}

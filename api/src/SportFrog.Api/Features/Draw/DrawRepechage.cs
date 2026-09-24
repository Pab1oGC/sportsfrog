using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Draws kyorugi's repechage: two bronze-medal ladders, one per finalist's
/// half of the draw, fought by whoever that finalist beat along the way.
/// </summary>
/// <remarks>
/// Its own operation rather than a step of <see cref="AdvanceBracket"/> or
/// <see cref="DrawCalendar"/>, for the same reason <see cref="PromoteClassification"/>
/// is not a flag on either: the final's two competitors are not known until
/// the semifinals say so, whichever of the two ways this category's bracket
/// reaches that point — drawn whole by <see cref="KnockoutCalendarDraw"/> or
/// advanced a round at a time by <see cref="AdvanceBracket"/> — so this has
/// to run after both, not as a parameter of either.
///
/// Everything it needs is already on the calendar: who the two finalists
/// beat, and in which order, is read straight back from their own earlier
/// matches rather than tracked separately anywhere, the same "derive, don't
/// duplicate" choice <see cref="ReadPublicCompetition"/>'s own champion
/// lookup already makes. See <see cref="Repechage"/> for the ladder itself.
/// </remarks>
public static class DrawRepechage
{
    /// <param name="Created">How many repechage matches this drew, across both halves.</param>
    /// <param name="AutomaticBronzeTeamIds">
    /// Finalists' halves settled with nobody left to fight — the sole
    /// competitor eligible on that side takes the bronze outright, the same
    /// way a lone bye advances without playing. Empty when both halves had
    /// enough entrants to need a match, or when a half had none at all.
    /// </param>
    public sealed record Response(int Created, IReadOnlyList<Guid> AutomaticBronzeTeamIds);

    public static IEndpointRouteBuilder MapDrawRepechage(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/draw/repechage", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(DrawRepechage))
            .WithSummary("Draws kyorugi's repechage once the final's two competitors are known.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        SportFrogDbContext database,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories
            .AsNoTracking()
            .Include(candidate => candidate.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category?.Competition is not { } competition)
        {
            return Results.NotFound();
        }

        if (!category.UsesRepechage)
        {
            return Results.Problem(
                detail: "Esta categoría no tiene habilitado el repechaje. Activalo al editar la " +
                        "categoría si esta competencia lo va a usar.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var final = await database.Matches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                match => match.CategoryId == categoryId
                    && match.Phase == Bracket.FinalPhase
                    && !match.IsRepechage,
                cancellationToken);

        if (final is null)
        {
            return Results.Problem(
                detail: "Esta categoría todavía no tiene una final sorteada. El repechaje se arma " +
                        "una vez que se conocen los dos finalistas.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (final.HomeTeamId is not { } finalistA || final.AwayTeamId is not { } finalistB)
        {
            return Results.Problem(
                detail: "La final de esta categoría todavía no tiene los dos finalistas definidos.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var existingRepechage = await database.Matches
            .Where(match => match.CategoryId == categoryId && match.IsRepechage)
            .ToListAsync(cancellationToken);

        if (existingRepechage.Any(match => match.Status is MatchState.Finished or MatchState.Walkover))
        {
            return Results.Problem(
                detail: "El repechaje de esta categoría ya tiene resultados cargados, así que no " +
                        "se puede volver a sortear.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var deletedAt = clock.GetUtcNow();

        foreach (var match in existingRepechage)
        {
            // Ordinary to redraw, same as a knockout's own calendar: nobody
            // has played yet, so nothing is orphaned by replacing it.
            match.DeletedAt = deletedAt;
        }

        var newMatches = new List<Match>();
        var automaticBronze = new List<Guid>();
        var finalists = new[] { finalistA, finalistB };

        for (var branch = 0; branch < finalists.Length; branch++)
        {
            var finalist = finalists[branch];
            var priorMatches = await database.Matches
                .AsNoTracking()
                .Where(match => match.CategoryId == categoryId
                    && match.Phase != null
                    && !match.IsRepechage
                    && match.Id != final.Id
                    && (match.HomeTeamId == finalist || match.AwayTeamId == finalist))
                .OrderBy(match => match.RoundNumber)
                .ToListAsync(cancellationToken);

            if (priorMatches.Any(match => match.Status is not (MatchState.Finished or MatchState.Walkover)))
            {
                return Results.Problem(
                    detail: "Todavía hay partidos sin resultado en el camino de uno de los " +
                            "finalistas, así que no se sabe a quién le corresponde el repechaje.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            // Whoever this finalist beat, oldest round first — the loser on
            // whichever side was not them.
            var beaten = priorMatches
                .Select(match => match.HomeTeamId == finalist ? match.AwayTeamId!.Value : match.HomeTeamId!.Value)
                .ToList();

            var half = Repechage.BuildHalf(beaten);

            if (half.AutomaticBronze is { } bronze)
            {
                automaticBronze.Add(bronze);
                continue;
            }

            if (half.Matches.Count == 0)
            {
                continue;
            }

            // Assigned up front so a later rung's HomeSourceMatchId/
            // AwaySourceMatchId can point at an earlier one's id before any
            // of them exist as a row — the same trick DrawCalendar plays for
            // the main bracket. Local to this half: the other half's own ids
            // are a completely separate list, since neither ladder ever
            // feeds the other.
            var ids = half.Matches.Select(_ => Guid.NewGuid()).ToList();

            newMatches.AddRange(half.Matches.Select((planned, index) => new Match
            {
                Id = ids[index],
                OrgId = organization.RequireOrganizationId(),
                CompetitionId = competition.Id,
                CategoryId = categoryId,
                HomeTeamId = planned.Home.TeamId,
                AwayTeamId = planned.Away.TeamId,
                HomeSourceMatchId = planned.Home.SourceMatchIndex is { } homeSource ? ids[homeSource] : null,
                AwaySourceMatchId = planned.Away.SourceMatchIndex is { } awaySource ? ids[awaySource] : null,
                RoundNumber = (short)planned.Round,
                Phase = planned.Phase,
                Status = MatchState.Scheduled,
                IsRepechage = true,
                RepechageBranch = (short)(branch + 1),
            }));
        }

        if (newMatches.Count == 0 && automaticBronze.Count == 0)
        {
            return Results.Problem(
                detail: "Ninguno de los dos finalistas venció a nadie antes de la final, así que " +
                        "no hay a quién convocar a un repechaje.",
                statusCode: StatusCodes.Status409Conflict);
        }

        database.Matches.AddRange(newMatches);

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(newMatches.Count, automaticBronze));
    }
}

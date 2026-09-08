using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Caching;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Performances;

/// <summary>
/// Opens a category's classification stage: one performance slot for every
/// team still competing, waiting to be judged.
/// </summary>
/// <remarks>
/// Not a draw the way <c>DrawCalendar</c> makes one. A classification stage
/// pairs nobody — every team performs once, independent of every other — so
/// there is no fixture to schedule, no round, no bracket position to derive.
/// What this does is closer to what happens the instant a league or a group
/// stage is drawn: a set of rows for a stage that has just begun, before
/// anything in it is decided.
/// </remarks>
public static class OpenClassificationStage
{
    /// <param name="Replaced">
    /// How many slots the reopening removed to make room — ordinary the same
    /// way a redrawn calendar is ordinary, and reported rather than refused.
    /// </param>
    public sealed record Response(int Created, int Replaced);

    public static IEndpointRouteBuilder MapOpenClassificationStage(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/performances/open", HandleAsync)
            // Same actor as drawing a calendar: this is running the
            // competition, not configuring it.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(OpenClassificationStage))
            .WithSummary("Opens a category's classification stage, one slot per team still competing.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        SportFrogDbContext database,
        OrganizationContext organization,
        TimeProvider clock,
        IPublicQueryCache publicCache,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories
            .AsNoTracking()
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
                        "etapa de clasificación que abrir.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (competition.Status is not (CompetitionState.Draft or CompetitionState.Scheduled))
        {
            return Results.Problem(
                detail: "Esta competencia ya está en curso, así que su etapa de clasificación " +
                        "no se puede volver a abrir.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Only teams still competing — the same rule DrawCalendar applies to
        // a fixture list.
        var teamIds = await database.Teams
            .AsNoTracking()
            .Where(team => team.CategoryId == categoryId && team.IsActive)
            .Select(team => team.Id)
            .ToListAsync(cancellationToken);

        if (teamIds.Count == 0)
        {
            return Results.Problem(
                detail: "Esta categoría no tiene ningún competidor inscripto todavía.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var existing = await database.Performances
            .Where(performance => performance.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        if (existing.Any(performance => performance.Status == PerformanceStatus.Scored))
        {
            // Something has already been judged under the current stage.
            // Reopening it would leave a score attached to a slot the new
            // stage does not contain — the same reason DrawCalendar refuses
            // a redraw once a match has finished.
            return Results.Problem(
                detail: "Ya se cargaron puntajes en esta categoría, así que su etapa de " +
                        "clasificación no se puede volver a abrir. Corregí lo que falte a mano.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var now = clock.GetUtcNow();

        foreach (var performance in existing)
        {
            // Struck rather than removed, same as a redrawn calendar's old
            // fixtures.
            performance.DeletedAt = now;
        }

        var organizationId = organization.RequireOrganizationId();

        database.Performances.AddRange(teamIds.Select(teamId => new Performance
        {
            Id = Guid.NewGuid(),
            OrgId = organizationId,
            CompetitionId = competition.Id,
            CategoryId = categoryId,
            TeamId = teamId,
        }));

        await database.SaveChangesAsync(cancellationToken);

        // The public classification page, if anyone published one for this
        // competition, described a stage that no longer exists the instant
        // this commits.
        publicCache.Invalidate(PublicCacheKey.Classification(competition.Id));

        return Results.Ok(new Response(teamIds.Count, existing.Count));
    }
}

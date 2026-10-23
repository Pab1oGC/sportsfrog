using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Lists the stages one competition's draw already gives it, with the
/// scheduling logistics declared for each.
/// </summary>
/// <remarks>
/// Which stages exist is never decided here — <see cref="CompetitionStageSeeding"/>
/// wrote exactly one row per stage <see cref="CompetitionStages.For"/> derives
/// from the competition's format and sport, the moment the competition was
/// created. This only reads them, for the screen that lets an organizer set
/// each stage's own pitches and margin — see
/// <see cref="UpdateCompetitionStageSchedule"/> for the correction.
/// </remarks>
public static class ReadCompetitionStages
{
    public sealed record Summary(string Kind, short? BufferMinutes, IReadOnlyList<Guid>? SpaceIds);

    public static IEndpointRouteBuilder MapReadCompetitionStages(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/competitions/{competitionId:guid}/stages", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(ReadCompetitionStages))
            .WithSummary("Lists a competition's stages and their scheduling settings.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid competitionId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (!await database.Competitions.AnyAsync(
                candidate => candidate.Id == competitionId, cancellationToken))
        {
            return Results.NotFound();
        }

        // WireEnum.Label is C#, not SQL: the rows come back with their raw
        // Kind first, and only then get turned into the wire string.
        var stages = await database.CompetitionStages
            .AsNoTracking()
            .Where(stage => stage.CompetitionId == competitionId)
            .OrderBy(stage => stage.Kind)
            .Select(stage => new { stage.Kind, stage.BufferMinutes, stage.SpaceIds })
            .ToListAsync(cancellationToken);

        return Results.Ok(stages
            .Select(stage => new Summary(WireEnum.Label(stage.Kind), stage.BufferMinutes, stage.SpaceIds))
            .ToList());
    }
}

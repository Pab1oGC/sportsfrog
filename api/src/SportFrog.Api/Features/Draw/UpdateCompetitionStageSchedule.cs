using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Corrects one stage's own pitches and margin between matches.
/// </summary>
/// <remarks>
/// Replaces <c>CompetitionContract.Settings.Schedule</c>, which this used to
/// live on as one answer for the whole competition. A tournament's group
/// stage rarely plays on the same pitches as its final, and a single
/// schedule had no way to say that — see <see cref="CompetitionStage"/>'s
/// own remarks. Narrower than a correction to the competition itself for the
/// same reason <c>UpdateCategoryStageRuleset</c> is: a screen that only
/// means to change where a stage is played should not have to resend
/// anything else about the competition to avoid losing it.
/// </remarks>
public static class UpdateCompetitionStageSchedule
{
    public sealed record Request(short? BufferMinutes, IReadOnlyList<Guid>? SpaceIds);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            // Same bounds CompetitionContractValidator's own Schedule rules
            // used to state.
            RuleFor(request => request.BufferMinutes)
                .InclusiveBetween((short)0, (short)120)
                .When(request => request.BufferMinutes.HasValue)
                .WithMessage("El margen entre partidos va de 0 a 120 minutos.");

            RuleFor(request => request.SpaceIds)
                .Must(spaceIds => spaceIds == null || spaceIds.All(id => id != Guid.Empty))
                .WithMessage("Cada cancha habilitada necesita un espacio válido.");
        }
    }

    public static IEndpointRouteBuilder MapUpdateCompetitionStageSchedule(this IEndpointRouteBuilder routes)
    {
        routes.MapPatch("/competitions/{competitionId:guid}/stages/{kind}/schedule", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(UpdateCompetitionStageSchedule))
            .WithSummary("Corrects one stage's own pitches and margin between matches.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid competitionId,
        string kind,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (!WireEnum.TryParse<StageKind>(kind, out var stageKind))
        {
            return Results.NotFound();
        }

        var stage = await database.CompetitionStages.SingleOrDefaultAsync(
            candidate => candidate.CompetitionId == competitionId && candidate.Kind == stageKind,
            cancellationToken);

        if (stage is null)
        {
            // La competencia no existe, o existe pero no tiene esta fase --
            // p.ej pedir "bracket" en una que se dibuja a liga.
            return Results.NotFound();
        }

        stage.BufferMinutes = request.BufferMinutes;
        // The venues module already refuses to remove a space a competition
        // schedules against, so a space named here cannot go stale — see
        // CompetitionContractValidator's own remark on the field this
        // replaces. Whether it exists at all is likewise not asked here:
        // one naming a space that was never real simply places nothing,
        // which ScheduleCalendar reports.
        stage.SpaceIds = request.SpaceIds?.ToArray();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Categories;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Declares, or clears, one stage's own reglamento.
/// </summary>
/// <remarks>
/// The feature this exists for: a volleyball league that plays its group
/// stage to three sets and its final to five. Before this, the only
/// override was <see cref="Category.RulesetId"/> — one reglamento for the
/// whole category — and the group stage and the final are the same
/// category, so there was no way to say that at all without rewriting the
/// group stage's own rules once the bracket needed different ones, which
/// <c>UpdateRuleset</c> already refuses the moment anything has been played
/// under them.
///
/// The refusal here is the same rule, asked one stage narrower: this
/// stage's reglamento stays open to a correction for as long as nothing has
/// been played under <em>it</em>, whatever has already been played under
/// another stage of the same category. See <see cref="CategoryStagePolicy"/>.
/// </remarks>
public static class UpdateCategoryStageRuleset
{
    public sealed record Request(Guid? RulesetId);

    public static IEndpointRouteBuilder MapUpdateCategoryStageRuleset(this IEndpointRouteBuilder routes)
    {
        routes.MapPatch("/categories/{categoryId:guid}/stages/{kind}/ruleset", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Category, "categoryId"))
            .WithName(nameof(UpdateCategoryStageRuleset))
            .WithSummary("Declares, or clears, one stage's own reglamento.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid categoryId,
        string kind,
        Request request,
        SportFrogDbContext database,
        CategoryPolicy categoryPolicy,
        CategoryStagePolicy stagePolicy,
        CancellationToken cancellationToken)
    {
        if (!WireEnum.TryParse<StageKind>(kind, out var stageKind))
        {
            return Results.NotFound();
        }

        var category = await database.Categories
            .AsNoTracking()
            .Include(candidate => candidate.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category?.Competition is not { } competition)
        {
            return Results.NotFound();
        }

        var stage = await database.CategoryStages.SingleOrDefaultAsync(
            candidate => candidate.CategoryId == categoryId && candidate.Kind == stageKind,
            cancellationToken);

        if (stage is null)
        {
            // La categoría existe, pero no juega esta fase -- por ejemplo,
            // pedir el reglamento de "groups" en una que se dibuja a liga.
            return Results.NotFound();
        }

        if (await categoryPolicy.InspectRulesetOverrideAsync(
                competition, request.RulesetId, cancellationToken) is { } violation)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [violation.Property] = [violation.Message],
            });
        }

        if (stage.RulesetId != request.RulesetId
            && await stagePolicy.HasPlayedMatchesAsync(categoryId, stageKind, cancellationToken))
        {
            return Results.Problem(
                detail: "Ya se jugaron partidos de esta fase, así que su reglamento quedó fijo: " +
                        "los resultados ya registrados se leen contra él, y reemplazarlo ahora " +
                        "los reescribiría sin que nadie edite un partido.",
                statusCode: StatusCodes.Status409Conflict);
        }

        stage.RulesetId = request.RulesetId;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Caching;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Categories;

/// <summary>
/// Moves a category in the order its competition lists them.
/// </summary>
/// <remarks>
/// Split out of <see cref="UpdateCategory"/> on purpose: reordering a list is
/// not a correction to who it admits, and <see cref="UpdateCategory"/>'s own
/// remarks already draw that line — the name and the display order are
/// labels, and labels get corrected regardless of the competition's status,
/// while everything else is eligibility a roster was already accepted
/// against. Asking a caller that only wants to move one category up or down
/// to resend the full contract is how a stale read of the other fields —
/// someone else's edit to who the category admits, loaded into this screen
/// before that edit happened — gets written back over it the moment this one
/// saves.
/// </remarks>
public static class UpdateCategoryDisplayOrder
{
    public sealed record Request(short DisplayOrder);

    public static IEndpointRouteBuilder MapUpdateCategoryDisplayOrder(this IEndpointRouteBuilder routes)
    {
        routes.MapPatch("/competitions/{competitionId:guid}/categories/{id:guid}/display-order", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(UpdateCategoryDisplayOrder))
            .WithSummary("Moves a category in the order its competition lists them.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid id,
        Request request,
        SportFrogDbContext database,
        IPublicQueryCache publicCache,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories.SingleOrDefaultAsync(
            candidate => candidate.CompetitionId == competitionId && candidate.Id == id,
            cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        category.DisplayOrder = request.DisplayOrder;

        await database.SaveChangesAsync(cancellationToken);

        // The public page lists a competition's divisions in this order —
        // see ReadPublicCompetition.Shell. Invalidating a key nobody cached
        // is a no-op.
        publicCache.Invalidate(PublicCacheKey.CompetitionPage(competitionId));

        return Results.NoContent();
    }
}

internal sealed class UpdateCategoryDisplayOrderValidator : AbstractValidator<UpdateCategoryDisplayOrder.Request>
{
    public UpdateCategoryDisplayOrderValidator()
    {
        // Same bound as CategoryContractValidator's own DisplayOrder rule:
        // the two have to agree, since this is a correction to the same
        // column under a narrower contract, not a different rule for it.
        RuleFor(request => request.DisplayOrder)
            .InclusiveBetween((short)0, (short)999);
    }
}

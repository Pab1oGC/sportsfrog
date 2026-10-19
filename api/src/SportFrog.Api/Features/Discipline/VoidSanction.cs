using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// Voids a sanction that should never have been raised, or undoes that.
/// </summary>
/// <remarks>
/// For the administrative mistake — the wrong person named, a duplicate
/// entry for the same incident — as opposed to <see cref="LiftSanction"/>,
/// which is for a sanction correctly raised but overturned on appeal. Both
/// leave the row and its original terms exactly as they were; see
/// <see cref="Sanction"/>'s own remarks for why neither ever edits one in
/// place.
/// </remarks>
public static class VoidSanction
{
    public sealed record Request(string? Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            // Mirrors ck_sanctions_void_reason — see WaiveSanctionFine's own
            // validator for why clearing needs no reason.
            RuleFor(request => request.Reason)
                .MaximumLength(300);
    }

    public static IEndpointRouteBuilder MapVoidSanction(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/competitions/{competitionId:guid}/sanctions/{id:guid}/void", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(VoidSanction))
            .WithSummary("Voids a sanction, or clears a void already on it.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid id,
        Request request,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var sanction = await database.Sanctions.SingleOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CompetitionId == competitionId, cancellationToken);

        if (sanction is null)
        {
            return Results.NotFound();
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        sanction.VoidedAt = reason is null ? null : clock.GetUtcNow();
        sanction.VoidReason = reason;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// Excuses a sanction's fine from needing to be paid, or undoes that.
/// </summary>
/// <remarks>
/// Only the fine term — a sanction cleared by matches and a fine
/// (<see cref="SanctionCompletionRule.Both"/>) still has to serve the
/// matches; this does not touch that half. The amount stands exactly as
/// imposed, the same reasoning <c>Features.Tuition.WaiveCharge</c> follows
/// for a pension charge: nothing here is ever edited in place, only marked.
/// </remarks>
public static class WaiveSanctionFine
{
    public sealed record Request(string? Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            // Mirrors ck_sanctions_fine_waiver_reason — clearing a waiver
            // needs no reason, since a reason explains why one exists, not
            // why one does not.
            RuleFor(request => request.Reason)
                .MaximumLength(300);
    }

    public static IEndpointRouteBuilder MapWaiveSanctionFine(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/competitions/{competitionId:guid}/sanctions/{id:guid}/fine-waiver", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(WaiveSanctionFine))
            .WithSummary("Waives a sanction's fine, or clears a waiver already on it.");

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

        if (reason is not null && sanction.FineAmount is null)
        {
            // Mirrors ck_sanctions_fine_waiver_requires_fine: a sanction
            // cleared by matches alone carries no fine to forgive.
            return Results.Problem(
                detail: "Esta sanción no lleva multa, así que no hay nada que condonar.",
                statusCode: StatusCodes.Status409Conflict);
        }

        sanction.FineWaivedAt = reason is null ? null : clock.GetUtcNow();
        sanction.FineWaiverReason = reason;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

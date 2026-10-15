using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Tuition;

/// <summary>
/// Voids a pension charge that should never have been raised, or undoes
/// that.
/// </summary>
/// <remarks>
/// For the accrual's own mistake — an enrollment the engine should not
/// have counted for a period — as opposed to <see cref="WaiveCharge"/>,
/// which is for a charge that was correctly raised but is not owed after
/// all. Both leave the row and its original amount exactly as they were;
/// see <see cref="TuitionCharge"/>'s remarks for why neither ever edits a
/// charge in place.
/// </remarks>
public static class VoidCharge
{
    public sealed record Request(string? Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            // Mirrors ck_tuition_charges_void_reason — see WaiveCharge's
            // own validator for why clearing needs no reason.
            RuleFor(request => request.Reason)
                .MaximumLength(300);
        }
    }

    public static IEndpointRouteBuilder MapVoidCharge(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/tuition/charges/{id:guid}/void", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.TuitionCharge, "id"))
            .WithName(nameof(VoidCharge))
            .WithSummary("Voids a pension charge, or clears a void already on it.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var charge = await database.TuitionCharges.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (charge is null)
        {
            return Results.NotFound();
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        charge.VoidedAt = reason is null ? null : clock.GetUtcNow();
        charge.VoidReason = reason;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Tuition;

/// <summary>
/// Excuses one pension charge from needing to be paid, or undoes that.
/// </summary>
/// <remarks>
/// A scholarship granted after the fact, or a charge raised for a month a
/// student was in truth excused from — the amount stands, exactly as
/// raised, and <see cref="TuitionCharge"/>'s own remarks are why: a charge
/// is never edited in place, only marked.
///
/// Independent of <see cref="VoidCharge"/>, the same way the entity's own
/// remarks describe: a charge voided after being waived still resolves
/// as voided, never as both.
/// </remarks>
public static class WaiveCharge
{
    public sealed record Request(string? Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            // Mirrors ck_tuition_charges_waiver_reason: clearing a waiver
            // needs no reason, since a reason explains why one exists, not
            // why one does not — the same rule SetEnrollmentFee already
            // follows for its own exception.
            RuleFor(request => request.Reason)
                .MaximumLength(300);
        }
    }

    public static IEndpointRouteBuilder MapWaiveCharge(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/tuition/charges/{id:guid}/waiver", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.TuitionCharge, "id"))
            .WithName(nameof(WaiveCharge))
            .WithSummary("Waives a pension charge, or clears a waiver already on it.");

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

        charge.WaivedAt = reason is null ? null : clock.GetUtcNow();
        charge.WaiverReason = reason;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

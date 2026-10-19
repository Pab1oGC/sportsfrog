using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// Voids a fine payment that should not count — entered against the wrong
/// sanction, duplicated, or simply never actually received.
/// </summary>
/// <remarks>
/// One-way, the same shape <c>Features.Tuition.VoidPayment</c> already is
/// for a pension payment: there is no "undo the void" here, because the fix
/// for a payment voided by mistake is recording it again, not un-voiding
/// evidence that something was wrong with it.
///
/// Independent of <see cref="ReceiptReviewState"/> — a payment can be
/// voided whether it was never reviewed, verified, or rejected. The moment
/// this runs, <c>SanctionLedger.Resolve</c> stops counting it toward the
/// fine's balance the next time anybody reads that sanction.
/// </remarks>
public static class VoidSanctionPayment
{
    public sealed record Request(string Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.Reason)
                .NotEmpty().WithMessage("El motivo es obligatorio.")
                .MaximumLength(300);
    }

    public static IEndpointRouteBuilder MapVoidSanctionPayment(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/competitions/{competitionId:guid}/sanction-payments/{id:guid}/void", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(VoidSanctionPayment))
            .WithSummary("Voids a sanction payment that should not count.");

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
        var payment = await database.SanctionPayments
            .Where(candidate => candidate.Id == id)
            .Where(candidate => candidate.Sanction!.CompetitionId == competitionId)
            .SingleOrDefaultAsync(cancellationToken);

        if (payment is null)
        {
            return Results.NotFound();
        }

        if (payment.VoidedAt is not null)
        {
            return Results.Problem(
                detail: "Este pago ya está anulado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        payment.VoidedAt = clock.GetUtcNow();
        payment.VoidReason = request.Reason.Trim();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

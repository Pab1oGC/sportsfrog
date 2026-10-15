using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Tuition;

/// <summary>
/// Voids a payment that should not count — entered against the wrong
/// charge, duplicated, or simply never actually received.
/// </summary>
/// <remarks>
/// One-way, unlike <see cref="WaiveCharge"/>/<see cref="VoidCharge"/>
/// back in the charges themselves: there is no "undo the void" here,
/// because the fix for a payment voided by mistake is recording it again,
/// not un-voiding evidence that something was wrong with it.
///
/// Voiding is independent of <see cref="ReceiptReviewState"/> — a payment
/// can be voided whether it was never reviewed, verified, or rejected. The
/// moment this runs, <c>TuitionLedger.Resolve</c> stops counting it toward
/// its charge's balance the next time anybody reads that charge, which is
/// the whole of what "returns the charge to partial automatically" means:
/// nothing here writes that status anywhere.
/// </remarks>
public static class VoidPayment
{
    public sealed record Request(string Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.Reason)
                .NotEmpty().WithMessage("El motivo es obligatorio.")
                .MaximumLength(300);
    }

    public static IEndpointRouteBuilder MapVoidPayment(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/payments/{id:guid}/void", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(VoidPayment))
            .WithSummary("Voids a payment that should not count.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var payment = await database.TuitionPayments.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

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

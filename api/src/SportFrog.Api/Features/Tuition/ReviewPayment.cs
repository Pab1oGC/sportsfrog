using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Tuition;

/// <summary>Checks a receipt against what the payment it backs claims.</summary>
/// <remarks>
/// Only ever reaches a payment that is still <see cref="ReceiptReviewState.Pending"/> —
/// a cash payment is born <see cref="ReceiptReviewState.Verified"/> and never
/// needs this, and a payment already reviewed is not reviewed a second
/// time here: a mistaken review is undone with <see cref="VoidPayment"/>
/// and a fresh <see cref="RecordPayment"/>, not by calling this again.
/// </remarks>
public static class ReviewPayment
{
    public sealed record Request(bool Verified, string? Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            // Mirrors ck_tuition_payments_review_reason: rejecting needs a
            // reason, verifying does not.
            RuleFor(request => request)
                .Must(request => request.Verified || !string.IsNullOrWhiteSpace(request.Reason))
                .WithMessage("Rechazar un comprobante necesita un motivo.");
    }

    public static IEndpointRouteBuilder MapReviewPayment(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/payments/{id:guid}/review", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(ReviewPayment))
            .WithSummary("Verifies or rejects a payment's receipt.");

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

        if (payment.ReviewState != ReceiptReviewState.Pending)
        {
            return Results.Problem(
                detail: "Este pago ya fue revisado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        payment.ReviewState = request.Verified ? ReceiptReviewState.Verified : ReceiptReviewState.Rejected;
        payment.ReviewReason = request.Verified ? null : request.Reason!.Trim();
        payment.ReviewedAt = clock.GetUtcNow();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

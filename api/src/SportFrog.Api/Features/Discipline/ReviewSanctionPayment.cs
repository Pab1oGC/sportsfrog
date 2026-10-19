using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Discipline;

/// <summary>Checks a fine payment's receipt against what it claims.</summary>
/// <remarks>
/// Only ever reaches a payment that is still <see cref="ReceiptReviewState.Pending"/>
/// — a cash payment is born <see cref="ReceiptReviewState.Verified"/> and
/// never needs this, and a payment already reviewed is not reviewed a
/// second time here: a mistaken review is undone with
/// <see cref="VoidSanctionPayment"/> and a fresh
/// <see cref="RecordSanctionPayment"/>, not by calling this again. The same
/// shape <c>Features.Tuition.ReviewPayment</c> already is for a pension
/// payment.
/// </remarks>
public static class ReviewSanctionPayment
{
    public sealed record Request(bool Verified, string? Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            // Mirrors ck_sanction_payments_review_reason: rejecting needs a
            // reason, verifying does not.
            RuleFor(request => request)
                .Must(request => request.Verified || !string.IsNullOrWhiteSpace(request.Reason))
                .WithMessage("Rechazar un comprobante necesita un motivo.");
    }

    public static IEndpointRouteBuilder MapReviewSanctionPayment(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/competitions/{competitionId:guid}/sanction-payments/{id:guid}/review", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(ReviewSanctionPayment))
            .WithSummary("Verifies or rejects a sanction payment's receipt.");

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

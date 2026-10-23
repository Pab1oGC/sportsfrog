using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Discipline;

/// <summary>Reads one sanction payment, with a signed link to its receipt.</summary>
/// <remarks>
/// <see cref="ReadSanctions.ListAsync"/> carries only <c>HasReceipt</c>, a
/// bare flag, never a link -- signing one for every payment of every
/// sanction in the competition on every page load would be wasted work
/// almost always. <see cref="ReadSanctions.ReadAsync"/> (one sanction) signs
/// its own small handful of payments directly, which is what
/// <c>SanctionPaymentsDialog</c> actually reads from; this exists as the
/// narrower read for a caller that only has one payment's id to go on, the
/// same shape <c>Features.Tuition.ReadPayment</c> already is for a pension
/// payment.
/// </remarks>
public static class ReadSanctionPayment
{
    public sealed record Response(
        Guid Id,
        Guid SanctionId,
        decimal Amount,
        PaymentMethod Method,
        string? ReceiptUrl,
        ReceiptReviewState ReviewState,
        string? ReviewReason,
        DateTimeOffset? ReviewedAt,
        DateTimeOffset? VoidedAt,
        string? VoidReason,
        DateTimeOffset CreatedAt);

    public static IEndpointRouteBuilder MapReadSanctionPayment(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/competitions/{competitionId:guid}/sanction-payments/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(ReadSanctionPayment))
            .WithSummary("Reads one sanction payment, with a signed link to its receipt.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid id,
        SportFrogDbContext database,
        SanctionReceipt receipts,
        CancellationToken cancellationToken)
    {
        var payment = await database.SanctionPayments
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Where(candidate => candidate.Sanction!.CompetitionId == competitionId)
            .SingleOrDefaultAsync(cancellationToken);

        if (payment is null)
        {
            return Results.NotFound();
        }

        var receiptUrl = await receipts.LinkAsync(payment.ReceiptKey, cancellationToken);

        return Results.Ok(new Response(
            payment.Id,
            payment.SanctionId,
            payment.Amount,
            payment.Method,
            receiptUrl,
            payment.ReviewState,
            payment.ReviewReason,
            payment.ReviewedAt,
            payment.VoidedAt,
            payment.VoidReason,
            payment.CreatedAt));
    }
}

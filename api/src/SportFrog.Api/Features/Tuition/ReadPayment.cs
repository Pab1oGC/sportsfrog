using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Tuition;

/// <summary>Reads one payment, with a signed link to its receipt if it has one.</summary>
public static class ReadPayment
{
    public sealed record Response(
        Guid Id,
        Guid ChargeId,
        decimal AmountBob,
        PaymentMethod Method,
        string? ReceiptUrl,
        ReceiptReviewState ReviewState,
        string? ReviewReason,
        DateTimeOffset? ReviewedAt,
        DateTimeOffset? VoidedAt,
        string? VoidReason,
        DateTimeOffset CreatedAt);

    public static IEndpointRouteBuilder MapReadPayment(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/payments/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadPayment))
            .WithSummary("Reads one payment and a signed link to its receipt.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        PaymentReceipt receipts,
        CancellationToken cancellationToken)
    {
        var payment = await database.TuitionPayments
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (payment is null)
        {
            return Results.NotFound();
        }

        var receiptUrl = await receipts.LinkAsync(payment.ReceiptKey, cancellationToken);

        return Results.Ok(new Response(
            payment.Id, payment.ChargeId, payment.AmountBob, payment.Method, receiptUrl,
            payment.ReviewState, payment.ReviewReason, payment.ReviewedAt,
            payment.VoidedAt, payment.VoidReason, payment.CreatedAt));
    }
}

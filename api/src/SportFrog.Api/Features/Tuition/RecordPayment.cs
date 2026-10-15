using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Tuition;

/// <summary>Records a payment against a pension charge.</summary>
/// <remarks>
/// No course scope on this endpoint, or on any other in this file —
/// whoever collects a payment is not necessarily whoever teaches the
/// course the charge happens to belong to, so narrowing by course would
/// refuse the one person this is actually for.
/// </remarks>
public static class RecordPayment
{
    public sealed record Request(decimal AmountBob, PaymentMethod Method, string? ReceiptDataUrl);

    public sealed record Response(Guid Id, ReceiptReviewState ReviewState);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.AmountBob)
                .GreaterThan(0).WithMessage("El monto tiene que ser mayor a cero.");

            RuleFor(request => request.Method)
                .IsInEnum().WithMessage("El método de pago no es válido.");

            RuleFor(request => request.ReceiptDataUrl)
                .Must(InlineReceipt.IsAcceptable)
                .WithMessage(InlineReceipt.Requirement);

            // Mirrors ck_payment_receipt.
            RuleFor(request => request)
                .Must(request => (request.Method == PaymentMethod.Cash) == (request.ReceiptDataUrl is null))
                .WithMessage("Un pago en efectivo no lleva comprobante, y cualquier otro método " +
                             "necesita uno.");
        }
    }

    public static IEndpointRouteBuilder MapRecordPayment(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/charges/{id:guid}/payments", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(RecordPayment))
            .WithSummary("Records a payment against a pension charge.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        PaymentPolicy policy,
        PaymentReceipt receipts,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var charge = await database.TuitionCharges.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (charge is null)
        {
            return Results.NotFound();
        }

        if (policy.InspectCharge(charge) is { } reason)
        {
            return Results.Problem(detail: reason, statusCode: StatusCodes.Status409Conflict);
        }

        string? receiptKey = null;

        if (request.ReceiptDataUrl is { } dataUrl)
        {
            receiptKey = await receipts.StoreAsync(charge.Id, dataUrl, cancellationToken);

            if (receiptKey is null)
            {
                return PaymentReceipt.NotAReceipt();
            }
        }

        var payment = new TuitionPayment
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            ChargeId = charge.Id,
            AmountBob = request.AmountBob,
            Method = request.Method,
            ReceiptKey = receiptKey,

            // The operator typing a cash payment in is the verification —
            // see this file's own remarks and TuitionPayment's.
            ReviewState = request.Method == PaymentMethod.Cash
                ? ReceiptReviewState.Verified
                : ReceiptReviewState.Pending,
        };

        // Added to its own set directly, not through a navigation
        // collection — see TuitionAccrual's own remark for why.
        database.TuitionPayments.Add(payment);

        await database.SaveChangesAsync(cancellationToken);

        return Results.Created($"/payments/{payment.Id}", new Response(payment.Id, payment.ReviewState));
    }
}

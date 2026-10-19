using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Discipline;

/// <summary>Records a payment against a sanction's fine.</summary>
public static class RecordSanctionPayment
{
    public sealed record Request(decimal Amount, PaymentMethod Method, string? ReceiptDataUrl);

    public sealed record Response(Guid Id, ReceiptReviewState ReviewState);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Amount)
                .GreaterThan(0).WithMessage("El monto tiene que ser mayor a cero.");

            RuleFor(request => request.Method)
                .IsInEnum().WithMessage("El método de pago no es válido.");

            RuleFor(request => request.ReceiptDataUrl)
                .Must(InlineReceipt.IsAcceptable)
                .WithMessage(InlineReceipt.Requirement);

            // Mirrors ck_sanction_payment_receipt.
            RuleFor(request => request)
                .Must(request => (request.Method == PaymentMethod.Cash) == (request.ReceiptDataUrl is null))
                .WithMessage("Un pago en efectivo no lleva comprobante, y cualquier otro método " +
                             "necesita uno.");
        }
    }

    public static IEndpointRouteBuilder MapRecordSanctionPayment(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/competitions/{competitionId:guid}/sanctions/{sanctionId:guid}/payments", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(RecordSanctionPayment))
            .WithSummary("Records a payment against a sanction's fine.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid sanctionId,
        Request request,
        SportFrogDbContext database,
        SanctionPaymentPolicy policy,
        SanctionReceipt receipts,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var sanction = await database.Sanctions.SingleOrDefaultAsync(
            candidate => candidate.Id == sanctionId && candidate.CompetitionId == competitionId,
            cancellationToken);

        if (sanction is null)
        {
            return Results.NotFound();
        }

        if (policy.InspectSanction(sanction) is { } reason)
        {
            return Results.Problem(detail: reason, statusCode: StatusCodes.Status409Conflict);
        }

        string? receiptKey = null;

        if (request.ReceiptDataUrl is { } dataUrl)
        {
            receiptKey = await receipts.StoreAsync(sanction.Id, dataUrl, cancellationToken);

            if (receiptKey is null)
            {
                return SanctionReceipt.NotAReceipt();
            }
        }

        var payment = new SanctionPayment
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            SanctionId = sanction.Id,
            Amount = request.Amount,
            Method = request.Method,
            ReceiptKey = receiptKey,

            // The operator typing a cash payment in is the verification —
            // same reasoning TuitionPayment follows for its own.
            ReviewState = request.Method == PaymentMethod.Cash
                ? ReceiptReviewState.Verified
                : ReceiptReviewState.Pending,
        };

        database.SanctionPayments.Add(payment);

        await database.SaveChangesAsync(cancellationToken);

        return Results.Created($"/sanction-payments/{payment.Id}", new Response(payment.Id, payment.ReviewState));
    }
}

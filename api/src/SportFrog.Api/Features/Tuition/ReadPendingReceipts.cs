using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Tuition;

/// <summary>The receipts tray: every payment still waiting to be checked, organization-wide.</summary>
/// <remarks>
/// Not scoped by course, same as every other endpoint in this file — see
/// <see cref="RecordPayment"/>'s own remarks for why. Whoever reviews a
/// receipt is reviewing a payment, not a course.
/// </remarks>
public static class ReadPendingReceipts
{
    public sealed record PendingReceipt(
        Guid Id,
        Guid ChargeId,
        decimal AmountBob,
        PaymentMethod Method,
        string? ReceiptUrl,
        DateOnly Period,
        string AthleteFirstName,
        string AthleteLastName,
        string CourseName,
        DateTimeOffset CreatedAt);

    public static IEndpointRouteBuilder MapReadPendingReceipts(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/payments/pending", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadPendingReceipts))
            .WithSummary("Lists every payment still waiting for its receipt to be checked.");

        return routes;
    }

    internal static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        PaymentReceipt receipts,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from payment in database.TuitionPayments
            where payment.ReviewState == ReceiptReviewState.Pending && payment.VoidedAt == null
            join charge in database.TuitionCharges on payment.ChargeId equals charge.Id
            join entry in database.CourseEnrollments on charge.EnrollmentId equals entry.Id
            join athlete in database.Athletes on entry.AthleteId equals athlete.Id
            join course in database.Courses on entry.CourseId equals course.Id
            orderby payment.CreatedAt
            select new
            {
                payment.Id,
                payment.ChargeId,
                payment.AmountBob,
                payment.Method,
                payment.ReceiptKey,
                payment.CreatedAt,
                charge.Period,
                athlete.FirstName,
                athlete.LastName,
                course.Name,
            })
            .ToListAsync(cancellationToken);

        // Signing is arithmetic, not a round trip -- see ReadAthletes'
        // own remark on PresentAsync for why a loop here is not the
        // problem it would look like for anything that left the process.
        var response = new List<PendingReceipt>(rows.Count);

        foreach (var row in rows)
        {
            var receiptUrl = await receipts.LinkAsync(row.ReceiptKey, cancellationToken);

            response.Add(new PendingReceipt(
                row.Id, row.ChargeId, row.AmountBob, row.Method, receiptUrl,
                row.Period, row.FirstName, row.LastName, row.Name, row.CreatedAt));
        }

        return Results.Ok(response);
    }
}

using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Discipline;

/// <summary>The receipts tray: every fine payment of a competition still waiting to be checked.</summary>
/// <remarks>
/// Scoped to one competition, unlike <c>Features.Tuition.ReadPendingReceipts</c>,
/// which reads organization-wide because a pension payment carries no
/// single competition to narrow it by. A sanction always does — see
/// <see cref="Sanction.CompetitionId"/> — so this stays inside the same
/// competition-scoped shape every other endpoint in this module already
/// takes.
/// </remarks>
public static class ReadPendingSanctionReceipts
{
    public sealed record PendingReceipt(
        Guid Id,
        Guid SanctionId,
        decimal Amount,
        PaymentMethod Method,
        string? ReceiptUrl,
        string AthleteFirstName,
        string AthleteLastName,
        string TeamName,
        DateTimeOffset CreatedAt);

    public static IEndpointRouteBuilder MapReadPendingSanctionReceipts(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/competitions/{competitionId:guid}/sanctions/pending-receipts", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(ReadPendingSanctionReceipts))
            .WithSummary("Lists every fine payment of the competition still waiting for its receipt to be checked.");

        return routes;
    }

    internal static async Task<IResult> ListAsync(
        Guid competitionId,
        SportFrogDbContext database,
        SanctionReceipt receipts,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from payment in database.SanctionPayments
            where payment.ReviewState == ReceiptReviewState.Pending && payment.VoidedAt == null
            join sanction in database.Sanctions on payment.SanctionId equals sanction.Id
            where sanction.CompetitionId == competitionId
            join athlete in database.Athletes on sanction.AthleteId equals athlete.Id
            join team in database.Teams on sanction.TeamId equals team.Id
            orderby payment.CreatedAt
            select new
            {
                payment.Id,
                payment.SanctionId,
                payment.Amount,
                payment.Method,
                payment.ReceiptKey,
                payment.CreatedAt,
                athlete.FirstName,
                athlete.LastName,
                team.Name,
            })
            .ToListAsync(cancellationToken);

        // Signing is arithmetic, not a round trip — see
        // Features.Tuition.ReadPendingReceipts' own remark on why a loop
        // here is not the problem it would look like for anything that left
        // the process.
        var response = new List<PendingReceipt>(rows.Count);

        foreach (var row in rows)
        {
            var receiptUrl = await receipts.LinkAsync(row.ReceiptKey, cancellationToken);

            response.Add(new PendingReceipt(
                row.Id, row.SanctionId, row.Amount, row.Method, receiptUrl,
                row.FirstName, row.LastName, row.Name, row.CreatedAt));
        }

        return Results.Ok(response);
    }
}

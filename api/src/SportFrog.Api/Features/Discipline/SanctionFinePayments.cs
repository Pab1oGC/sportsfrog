using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// How much of each sanction's fine still counts as paid.
/// </summary>
/// <remarks>
/// The narrow half of what <see cref="ReadSanctions"/> computes for itself —
/// that endpoint also needs each payment's own history (method, review
/// state, receipt) to show a person their statement, which this does not
/// attempt to duplicate. This exists for callers that only need the one
/// number <see cref="SportFrog.Domain.Discipline.SanctionLedger.Resolve"/>
/// asks for — <see cref="Features.Documents.ScanCredential"/> is the first.
///
/// Batched across every sanction asked about in one call, the same shape
/// <see cref="SanctionProgress.MatchesServedAsync"/> already is for matches
/// served.
/// </remarks>
internal sealed class SanctionFinePayments(SportFrogDbContext database)
{
    public async Task<IReadOnlyDictionary<Guid, decimal>> PaidAsync(
        IReadOnlyCollection<Guid> sanctionIds, CancellationToken cancellationToken)
    {
        if (sanctionIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        // Only what still counts toward the balance — a rejected receipt
        // never did, and a voided payment stops the moment it is voided,
        // the same filter ReadSanctions applies to the same rows.
        var paid = await database.SanctionPayments
            .AsNoTracking()
            .Where(payment => sanctionIds.Contains(payment.SanctionId))
            .Where(payment => payment.VoidedAt == null && payment.ReviewState != ReceiptReviewState.Rejected)
            .GroupBy(payment => payment.SanctionId)
            .Select(group => new { SanctionId = group.Key, Paid = group.Sum(payment => payment.Amount) })
            .ToDictionaryAsync(group => group.SanctionId, group => group.Paid, cancellationToken);

        // Every id answers, even one with nothing paid at all — callers
        // index this dictionary unconditionally, the same way
        // MatchesServedAsync's own result always has an entry for each
        // sanction asked about.
        foreach (var id in sanctionIds)
        {
            paid.TryAdd(id, 0m);
        }

        return paid;
    }
}

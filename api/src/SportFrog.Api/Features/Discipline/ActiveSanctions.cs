using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Discipline;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// One sanction, resolved, that is currently active against one of the
/// people asked about.
/// </summary>
/// <remarks>
/// Null on <see cref="MatchesRequired"/>/<see cref="MatchesServed"/> mirrors
/// <see cref="ReadSanctions.Response"/>'s own convention: absent where the
/// sanction's <see cref="CompletionRule"/> carries no matches term at all,
/// not merely zero.
/// </remarks>
public sealed record ActiveSanctionDetail(
    Guid SanctionId,
    Guid AthleteId,
    string AthleteFirstName,
    string AthleteLastName,
    string Reason,
    SanctionCompletionRule CompletionRule,
    int? MatchesRequired,
    int? MatchesServed,
    int? MatchesRemaining,
    decimal? FineAmount,
    decimal? FineBalance,
    bool CredentialWithheld);

/// <summary>
/// Which of a set of people currently carry a sanction that blocks them,
/// for one competition.
/// </summary>
/// <remarks>
/// The one place this question is answered from a full table read rather
/// than the public endpoint's own bare boolean
/// (<c>sanction_blocks_entry</c>) — an authenticated reader is trusted with
/// why, not merely whether. <see cref="Documents.ScanCredential"/> asks this
/// about one person at a time; <see cref="Matches.RecordResult"/> asks it
/// about both of a match's rosters at once, which is the whole reason this
/// takes a collection rather than a single id — one batched read for
/// however many people are asked about, the same shape
/// <see cref="SanctionProgress.MatchesServedAsync"/> already takes for
/// matches served.
/// </remarks>
internal sealed class ActiveSanctions(
    SportFrogDbContext database, SanctionProgress progress, SanctionFinePayments finePayments)
{
    public async Task<IReadOnlyList<ActiveSanctionDetail>> ForAthletesAsync(
        Guid competitionId, IReadOnlyCollection<Guid> athleteIds, CancellationToken cancellationToken)
    {
        if (athleteIds.Count == 0)
        {
            return [];
        }

        var sanctions = await database.Sanctions
            .AsNoTracking()
            .Where(sanction => sanction.CompetitionId == competitionId && athleteIds.Contains(sanction.AthleteId))

            // Lifted and voided never block — see SanctionLedger's own
            // precedence — so there is nothing for either to resolve here.
            .Where(sanction => sanction.VoidedAt == null && sanction.LiftedAt == null)
            .Include(sanction => sanction.Athlete)
            .ToListAsync(cancellationToken);

        if (sanctions.Count == 0)
        {
            return [];
        }

        var matchesServed = await progress.MatchesServedAsync(competitionId, sanctions, cancellationToken);
        var finePaid = await finePayments.PaidAsync(
            sanctions.Select(sanction => sanction.Id).ToList(), cancellationToken);

        var active = new List<ActiveSanctionDetail>(sanctions.Count);

        foreach (var sanction in sanctions)
        {
            var resolution = SanctionLedger.Resolve(
                sanction.CompletionRule,
                sanction.MatchesRequired,
                matchesServed[sanction.Id],
                sanction.FineAmount,
                finePaid[sanction.Id],
                fineWaived: sanction.FineWaivedAt is not null,
                lifted: false,
                voided: false);

            if (resolution.Status != SanctionStatus.Active)
            {
                continue;
            }

            active.Add(new ActiveSanctionDetail(
                sanction.Id,
                sanction.AthleteId,
                sanction.Athlete!.FirstName,
                sanction.Athlete.LastName,
                sanction.Reason,
                sanction.CompletionRule,
                sanction.MatchesRequired,
                sanction.CompletionRule == SanctionCompletionRule.Fine ? null : matchesServed[sanction.Id],
                resolution.MatchesRemaining,
                sanction.FineAmount,
                resolution.FineBalance,
                sanction.CredentialWithheldAt is not null));
        }

        return active;
    }
}

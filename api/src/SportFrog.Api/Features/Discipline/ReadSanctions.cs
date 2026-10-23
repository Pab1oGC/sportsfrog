using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Domain.Discipline;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// Reads a competition's sanctions, or one of them, each resolved to a
/// status.
/// </summary>
/// <remarks>
/// <see cref="SanctionLedger"/> is the one place that decides
/// Active/Served/Lifted/Voided, computed fresh every time from a sanction's
/// own terms against whatever has actually been served — the same shape
/// <c>Features.Tuition.ReadTuitionAccount</c> already reads a pension
/// charge's status in.
/// </remarks>
public static class ReadSanctions
{
    /// <summary>
    /// One payment against a sanction's fine — the discipline module's
    /// counterpart to <c>Features.Tuition.ReadTuitionAccount.Payment</c>.
    /// </summary>
    /// <param name="ReceiptUrl">
    /// A signed link to the stored receipt, or null. Set only by
    /// <see cref="ReadAsync"/> (one sanction, a handful of payments at
    /// most) — never by <see cref="ListAsync"/>, which would otherwise sign
    /// a link for every payment of every sanction in the competition on
    /// every page load, almost all of them never opened.
    /// <see cref="HasReceipt"/> stays true either way: it is what a caller
    /// reading the list checks before asking <c>ReadAsync</c> for the one
    /// sanction it actually wants to look at.
    /// </param>
    public sealed record Payment(
        Guid Id,
        decimal Amount,
        PaymentMethod Method,
        ReceiptReviewState ReviewState,
        string? ReviewReason,
        DateTimeOffset? VoidedAt,
        string? VoidReason,
        bool HasReceipt,
        string? ReceiptUrl,
        DateTimeOffset CreatedAt);

    public sealed record Response(
        Guid Id,
        Guid AthleteId,
        string AthleteFirstName,
        string AthleteLastName,
        Guid TeamId,
        string TeamName,
        Guid? MatchId,
        string Reason,
        SanctionCompletionRule CompletionRule,
        int? MatchesRequired,
        int? MatchesServed,
        int? MatchesRemaining,
        decimal? FineAmount,
        decimal? FineBalance,
        DateTimeOffset? FineWaivedAt,
        string? FineWaiverReason,
        DateTimeOffset? CredentialWithheldAt,
        Guid ImposedBy,
        DateTimeOffset ImposedAt,
        DateTimeOffset? LiftedAt,
        string? LiftReason,
        DateTimeOffset? VoidedAt,
        string? VoidReason,
        SanctionStatus Status,
        bool BlocksEntry,
        IReadOnlyList<Payment> Payments);

    public static IEndpointRouteBuilder MapReadSanctions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/competitions/{competitionId:guid}/sanctions", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(ReadSanctions))
            .WithSummary("Lists a competition's sanctions.");

        routes.MapGet("/competitions/{competitionId:guid}/sanctions/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName("ReadSanction")
            .WithSummary("Reads one sanction.");

        return routes;
    }

    /// <summary>
    /// Every sanction of the competition, most recent first.
    /// </summary>
    /// <remarks>
    /// Unfiltered by default — a voided or already-served sanction stays
    /// part of the record, the same reason a withdrawn registration stays on
    /// a squad list. <paramref name="athleteId"/> and <paramref name="teamId"/>
    /// narrow it to one person's or one team's history, which is how this is
    /// read in practice: from a person's own page, or a team's own bench.
    /// </remarks>
    internal static async Task<IResult> ListAsync(
        Guid competitionId,
        SportFrogDbContext database,
        SanctionProgress progress,
        CancellationToken cancellationToken,
        Guid? athleteId = null,
        Guid? teamId = null)
    {
        if (!await database.Competitions.AnyAsync(
                candidate => candidate.Id == competitionId, cancellationToken))
        {
            return Results.NotFound();
        }

        var sanctions = await database.Sanctions
            .AsNoTracking()
            .Where(sanction => sanction.CompetitionId == competitionId)
            .Where(sanction => athleteId == null || sanction.AthleteId == athleteId)
            .Where(sanction => teamId == null || sanction.TeamId == teamId)
            .OrderByDescending(sanction => sanction.ImposedAt)
            .Include(sanction => sanction.Athlete)
            .Include(sanction => sanction.Team)
            .ToListAsync(cancellationToken);

        var matchesServed = await progress.MatchesServedAsync(competitionId, sanctions, cancellationToken);
        var paymentsBySanction = await PaymentsBySanctionAsync(
            database, sanctions.Select(sanction => sanction.Id).ToList(), cancellationToken);

        // receipts: null -- see Payment.ReceiptUrl's own remarks for why a
        // list never signs one.
        var responses = new List<Response>(sanctions.Count);

        foreach (var sanction in sanctions)
        {
            responses.Add(await ProjectAsync(
                sanction, matchesServed[sanction.Id], paymentsBySanction[sanction.Id], receipts: null, cancellationToken));
        }

        return Results.Ok(responses);
    }

    internal static async Task<IResult> ReadAsync(
        Guid competitionId,
        Guid id,
        SportFrogDbContext database,
        SanctionProgress progress,
        SanctionReceipt receipts,
        CancellationToken cancellationToken)
    {
        var sanction = await database.Sanctions
            .AsNoTracking()
            .Where(candidate => candidate.Id == id && candidate.CompetitionId == competitionId)
            .Include(candidate => candidate.Athlete)
            .Include(candidate => candidate.Team)
            .SingleOrDefaultAsync(cancellationToken);

        if (sanction is null)
        {
            return Results.NotFound();
        }

        var matchesServed = await progress.MatchesServedAsync(competitionId, [sanction], cancellationToken);
        var paymentsBySanction = await PaymentsBySanctionAsync(database, [sanction.Id], cancellationToken);

        // receipts: signed here -- one sanction's own payments are few
        // enough that signing every one of them is cheap, and this is
        // exactly the read SanctionPaymentsDialog asks for when it wants to
        // show a receipt without a second round trip per row.
        return Results.Ok(await ProjectAsync(
            sanction, matchesServed[sanction.Id], paymentsBySanction[sanction.Id], receipts, cancellationToken));
    }

    /// <summary>
    /// Every sanction's own fine payments, batched in one read — the same
    /// reason <see cref="SanctionProgress.MatchesServedAsync"/> batches
    /// matches instead of asking per sanction.
    /// </summary>
    private static async Task<IReadOnlyDictionary<Guid, List<SanctionPayment>>> PaymentsBySanctionAsync(
        SportFrogDbContext database, IReadOnlyCollection<Guid> sanctionIds, CancellationToken cancellationToken)
    {
        if (sanctionIds.Count == 0)
        {
            return new Dictionary<Guid, List<SanctionPayment>>();
        }

        var byId = (await database.SanctionPayments
                .AsNoTracking()
                .Where(payment => sanctionIds.Contains(payment.SanctionId))
                .OrderBy(payment => payment.CreatedAt)
                .ToListAsync(cancellationToken))
            .GroupBy(payment => payment.SanctionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        // Every id answers, even one with no payments at all — Project
        // indexes this dictionary unconditionally, the same way
        // MatchesServedAsync's own result always has an entry for each
        // sanction asked about.
        foreach (var id in sanctionIds)
        {
            byId.TryAdd(id, []);
        }

        return byId;
    }

    /// <summary>
    /// Resolves one sanction and shapes it into the one answer both reads
    /// give, so they cannot drift into describing the same sanction
    /// differently.
    /// </summary>
    /// <remarks>
    /// <paramref name="matchesServed"/> and <paramref name="ownPayments"/>
    /// arrive already read — <see cref="SanctionProgress"/> and
    /// <see cref="PaymentsBySanctionAsync"/> are what count them, each in one
    /// batched read for however many sanctions are being projected at once.
    ///
    /// Only what still counts toward the balance feeds the resolver — a
    /// rejected receipt never did, and a voided payment stops the moment it
    /// is voided — the same filter <c>Features.Tuition.ReadTuitionAccount</c>
    /// already applies to a pension charge's own payments.
    /// </remarks>
    private static async Task<Response> ProjectAsync(
        Sanction sanction, int matchesServed, List<SanctionPayment> ownPayments,
        SanctionReceipt? receipts, CancellationToken cancellationToken)
    {
        var finePaid = ownPayments
            .Where(payment => payment.VoidedAt is null && payment.ReviewState != ReceiptReviewState.Rejected)
            .Sum(payment => payment.Amount);

        var resolution = SanctionLedger.Resolve(
            sanction.CompletionRule,
            sanction.MatchesRequired,
            matchesServed,
            sanction.FineAmount,
            finePaid,
            fineWaived: sanction.FineWaivedAt is not null,
            lifted: sanction.LiftedAt is not null,
            voided: sanction.VoidedAt is not null);

        var payments = new List<Payment>(ownPayments.Count);

        foreach (var payment in ownPayments)
        {
            var receiptUrl = receipts is null
                ? null
                : await receipts.LinkAsync(payment.ReceiptKey, cancellationToken);

            payments.Add(new Payment(
                payment.Id, payment.Amount, payment.Method, payment.ReviewState,
                payment.ReviewReason, payment.VoidedAt, payment.VoidReason,
                payment.ReceiptKey is not null, receiptUrl, payment.CreatedAt));
        }

        return new Response(
            sanction.Id,
            sanction.AthleteId,
            sanction.Athlete!.FirstName,
            sanction.Athlete.LastName,
            sanction.TeamId,
            sanction.Team!.Name,
            sanction.MatchId,
            sanction.Reason,
            sanction.CompletionRule,
            sanction.MatchesRequired,
            sanction.CompletionRule == SanctionCompletionRule.Fine ? null : matchesServed,
            resolution.MatchesRemaining,
            sanction.FineAmount,
            resolution.FineBalance,
            sanction.FineWaivedAt,
            sanction.FineWaiverReason,
            sanction.CredentialWithheldAt,
            sanction.ImposedBy,
            sanction.ImposedAt,
            sanction.LiftedAt,
            sanction.LiftReason,
            sanction.VoidedAt,
            sanction.VoidReason,
            resolution.Status,
            resolution.BlocksEntry,
            payments);
    }
}

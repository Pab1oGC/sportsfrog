using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Public;

/// <summary>What a scanned document turns out to be.</summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<VerificationState>))]
public enum VerificationState
{
    /// <summary>Issued, not withdrawn, and within its dates.</summary>
    Valid,

    /// <summary>Withdrawn by the organization. Real, and no longer good.</summary>
    Revoked,

    /// <summary>Its period has passed.</summary>
    Expired,

    /// <summary>Its period has not started.</summary>
    NotYetValid,

    /// <summary>
    /// Real, current, and not usable today because the person it names has
    /// an unresolved sanction — see <c>SportFrog.Domain.Discipline.SanctionLedger</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Response.ActiveSanctions"/> says which kind — by matches,
    /// by a fine, or both, and how much of each remains — because a steward
    /// (or anybody else holding the card) is expected to act on that, not
    /// merely be told the door is shut. What stays withheld is the free-text
    /// reason the sanction carries: that is the actual disciplinary story
    /// behind it, an internal matter the same way a revocation's own reason
    /// is (RNF-16), not the shape of what still blocks entry today.
    /// </remarks>
    Blocked,
}

/// <summary>
/// Answers whether a printed document is real, to whoever is holding it.
/// </summary>
/// <remarks>
/// The endpoint the QR code on every card leads to, and the only one on this
/// API that anybody on the internet can reach without a slug pair they were
/// given (RF-45). Two decisions shape it, and both are about what it does not
/// say.
///
/// It does not let anybody walk the list. The serial is sixty bits of
/// randomness rather than a counter precisely so that scanning one card tells
/// you nothing about the next; without that, this endpoint would be a way to
/// enumerate every child in a league.
///
/// And it says nothing the card does not already say. Whoever scanned it is
/// holding the thing: the name and the photograph are printed on it, so
/// naming the holder adds nothing and lets a referee confirm the card belongs
/// to the person in front of them. A credential's visible id is the same
/// reasoning — it is the one piece of text a steward actually reads off the
/// card and checks against a printed list, so echoing it back lets them
/// confirm they scanned the card they meant to. The identity document, the
/// date of birth, the guardian's contact and the photograph itself are none
/// of a scanner's business and are not here (RNF-16) — and neither is the
/// reason a document was revoked, which is an internal matter about a person
/// and not part of the answer "this card is no longer valid".
///
/// <see cref="Response.ActiveSanctions"/> is the one deliberate exception to
/// "nothing the card does not already say": a person standing at a gate
/// needs to know what is actually blocking someone to do anything about it,
/// not just that something is. It names the kind of sanction and how much
/// of it remains — never the free-text reason behind it, which stays exactly
/// as internal as a revocation's own reason. See
/// <c>public_active_sanctions</c> (AddPublicSanctionDetail) for the
/// SECURITY DEFINER function this reads through, the same shape
/// <c>sanction_blocks_entry</c> already used for the bare boolean this
/// replaces.
/// </remarks>
public static class VerifyDocument
{
    /// <summary>
    /// One sanction still blocking entry, reduced to what anybody holding
    /// the card may see.
    /// </summary>
    /// <remarks>
    /// The public counterpart to <c>Features.Documents.ScanCredential.ActiveSanction</c>
    /// — same shape, minus <c>Id</c>, <c>AthleteId</c> and, above all,
    /// <c>Reason</c>: the sanction's own disciplinary story stays an
    /// authenticated-only read.
    /// </remarks>
    public sealed record PublicActiveSanction(
        SanctionCompletionRule CompletionRule,

        /// <summary>Null on a sanction cleared by a fine alone.</summary>
        int? MatchesRemaining,

        /// <summary>Null on a sanction cleared by matches alone.</summary>
        decimal? FineBalance,

        bool CredentialWithheld);

    public sealed record Response(
        string SerialNumber,

        /// <summary>
        /// A credential's own printed identifier — a delegation code and a
        /// number, e.g. "IND-0042". Null on a certificate, and on a
        /// credential issued before this identifier existed.
        /// </summary>
        string? VisibleId,

        DocumentKind Kind,
        VerificationState Status,
        string Holder,
        string? Team,
        string? Club,
        string? Category,
        string Competition,
        string Season,
        string Organization,
        string? CertificateType,
        DateOnly? ValidFrom,
        DateOnly? ValidTo,
        DateOnly IssuedOn,
        DateOnly? RevokedOn,

        /// <summary>Empty unless <see cref="Status"/> is <see cref="VerificationState.Blocked"/>.</summary>
        IReadOnlyList<PublicActiveSanction> ActiveSanctions);

    public static IEndpointRouteBuilder MapVerifyDocument(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/public/verify/{organizationSlug}/{serial}", HandleAsync)
            .AsPublicReading()
            .WithName(nameof(VerifyDocument))
            .WithSummary("Says whether a printed credential or certificate is valid.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        string organizationSlug,
        string serial,
        PublicDocumentReader reader,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var answer = await reader.ReadAsync(
            organizationSlug,
            serial,
            async (database, resolved) =>
            {
                var document = await database.IssuedDocuments
                    .AsNoTracking()
                    .Where(candidate => candidate.Id == resolved.DocumentId)
                    .Select(candidate => new
                    {
                        candidate.SerialNumber,
                        candidate.VisibleId,
                        candidate.Kind,
                        candidate.Status,
                        candidate.AthleteId,
                        candidate.CompetitionId,
                        Holder = candidate.Athlete!.LastName + " " + candidate.Athlete.FirstName,
                        Team = candidate.Team!.Name,
                        Club = candidate.Team.Club!.Name,
                        Category = candidate.Team.Category!.Name,
                        Competition = candidate.Competition!.Name,
                        candidate.Competition.Season,
                        candidate.CertificateType,
                        candidate.ValidFrom,
                        candidate.ValidTo,
                        candidate.IssuedAt,
                        candidate.RevokedAt,
                    })

                    // The resolver already proved the row exists inside this
                    // same transaction.
                    .SingleAsync(cancellationToken);

                var organization = await database.Organizations
                    .AsNoTracking()
                    .Where(candidate => candidate.Id == resolved.OrganizationId)
                    .Select(candidate => candidate.Name)
                    .SingleAsync(cancellationToken);

                var status = State(document.Status, document.ValidFrom, document.ValidTo, today);
                var activeSanctions = new List<PublicActiveSanction>();

                // Only asked where it could change the answer: a document
                // already revoked or expired needs no second reason, and a
                // document issued to a team names nobody a sanction could
                // name either. sportfrog_public has no grant on sanctions
                // itself — see public_active_sanctions' own migration
                // (AddPublicSanctionDetail) for what crosses that line
                // instead, and for why this reads a SECURITY DEFINER
                // function rather than the tables directly.
                if (status == VerificationState.Valid && document.AthleteId is { } athleteId)
                {
                    activeSanctions = await database.Database
                        .SqlQuery<PublicActiveSanction>(
                            $"""
                             SELECT
                                 completion_rule AS "CompletionRule",
                                 matches_remaining AS "MatchesRemaining",
                                 fine_balance AS "FineBalance",
                                 credential_withheld AS "CredentialWithheld"
                             FROM public_active_sanctions({athleteId}, {document.CompetitionId})
                             """)
                        .ToListAsync(cancellationToken);

                    if (activeSanctions.Count > 0)
                    {
                        status = VerificationState.Blocked;
                    }
                }

                return new Response(
                    document.SerialNumber,
                    document.VisibleId,
                    document.Kind,
                    status,

                    // A document issued to a team has no athlete, so the team
                    // is the holder. Both cannot be absent: the schema forbids
                    // a document about nobody.
                    string.IsNullOrWhiteSpace(document.Holder)
                        ? document.Team ?? string.Empty
                        : document.Holder,
                    document.Team,
                    document.Club,
                    document.Category,
                    document.Competition,
                    document.Season,
                    organization,
                    document.CertificateType,
                    document.ValidFrom,
                    document.ValidTo,
                    DateOnly.FromDateTime(document.IssuedAt.UtcDateTime),
                    document.RevokedAt is { } revoked
                        ? DateOnly.FromDateTime(revoked.UtcDateTime)
                        : null,
                    activeSanctions);
            },
            cancellationToken);

        // A serial nobody issued, and one belonging to an organization that is
        // no longer active, answer identically. Telling them apart is what
        // somebody guessing at addresses is trying to do.
        return answer is null ? Results.NotFound() : Results.Ok(answer);
    }

    /// <summary>
    /// What the card is worth today.
    /// </summary>
    /// <remarks>
    /// Revocation outranks the dates. A credential withdrawn in October and
    /// valid on paper until December is withdrawn, and answering "expired" in
    /// January would quietly turn a disciplinary decision into the calendar
    /// running out.
    ///
    /// A document with no dates never expires, which is right for a
    /// certificate: taking part in the 2026 season is not something that stops
    /// being true.
    ///
    /// Internal rather than private: <c>Features.Documents.ScanCredential</c>,
    /// the authenticated counterpart reached by a steward's own session,
    /// reuses this exact mapping so the two never answer a document's own
    /// dates and revocation differently — the photograph, the revocation's
    /// own reason, and a sanction's free-text reason are the only things
    /// the authenticated side still says that this one does not.
    /// </remarks>
    internal static VerificationState State(
        DocumentState status,
        DateOnly? from,
        DateOnly? to,
        DateOnly today) =>
        status switch
        {
            DocumentState.Revoked => VerificationState.Revoked,
            _ when from is { } starts && today < starts => VerificationState.NotYetValid,
            _ when to is { } ends && today > ends => VerificationState.Expired,
            _ => VerificationState.Valid,
        };
}

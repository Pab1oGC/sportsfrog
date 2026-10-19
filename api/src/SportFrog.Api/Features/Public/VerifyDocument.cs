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
    /// Says only that, never why: the reason behind a sanction is exactly
    /// the disciplinary detail this endpoint already refuses for a
    /// revocation, and extending that refusal here is the whole point of
    /// answering a state rather than the sanction itself.
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
/// </remarks>
public static class VerifyDocument
{
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
        DateOnly? RevokedOn);

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

                // Only asked where it could change the answer: a document
                // already revoked or expired needs no second reason, and a
                // document issued to a team names nobody a sanction could
                // name either. sportfrog_public has no grant on sanctions
                // itself — see this call's own migration for why a bare
                // boolean is what crosses that line instead.
                if (status == VerificationState.Valid && document.AthleteId is { } athleteId)
                {
                    var blocked = await database.Database
                        .SqlQuery<bool>(
                            $"""
                             SELECT sanction_blocks_entry({athleteId}, {document.CompetitionId}) AS "Value"
                             """)
                        .SingleAsync(cancellationToken);

                    if (blocked)
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
                        : null);
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
    /// dates and revocation differently — only in how (or whether) a
    /// sanction is layered on top does the authenticated side say more.
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

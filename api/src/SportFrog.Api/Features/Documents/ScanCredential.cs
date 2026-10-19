using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Features.Discipline;
using SportFrog.Api.Features.Public;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Discipline;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Answers whether a printed document is valid, and what it names — the
/// authenticated counterpart to the QR code's own public answer.
/// </summary>
/// <remarks>
/// <see cref="Public.VerifyDocument"/> is the one endpoint on this API
/// anybody on the internet can reach, and it is built never to say more
/// than the card already does (RNF-16): no photograph, no date of birth, no
/// reason behind a revocation or a sanction. This is the other half of the
/// same QR code, reached once instead by somebody already signed in to the
/// organization that issued the card — a steward at a gate, checking who is
/// standing in front of them — and for that reader the restraint the public
/// endpoint keeps would be withholding exactly what the question was asked
/// for.
///
/// So this says the rest: the photograph printed on the card, to compare
/// against the face holding it; why a revoked document was revoked; and,
/// when the person it names carries an unresolved sanction for this same
/// competition, what the sanction is and how far it is from being served —
/// not merely the bare "no habilitada" the public endpoint answers through
/// <c>sanction_blocks_entry</c>.
///
/// Reachable with nothing narrower than organization membership: a gate at a
/// venue is not one team's or one club's own concern, and scoping this would
/// refuse exactly the person the role already trusts to read a roster.
/// </remarks>
public static class ScanCredential
{
    public sealed record ActiveSanction(
        Guid Id,
        string Reason,
        SanctionCompletionRule CompletionRule,

        /// <summary>Null on a sanction cleared by a fine alone — see <see cref="SportFrog.Api.Features.Discipline.ReadSanctions.Response"/> for the same convention.</summary>
        int? MatchesRequired,
        int? MatchesServed,
        int? MatchesRemaining,
        decimal? FineAmount,
        decimal? FineBalance,
        bool CredentialWithheld);

    public sealed record Response(
        string SerialNumber,
        string? VisibleId,
        DocumentKind Kind,
        VerificationState DocumentStatus,

        /// <summary>Why it was revoked. Null unless <see cref="DocumentStatus"/> is <see cref="VerificationState.Revoked"/>. Never shown to the public endpoint's own caller.</summary>
        string? RevocationReason,
        string? HolderFirstName,
        string? HolderLastName,
        string? PhotoUrl,
        string? Team,
        string? Club,
        string? Category,
        string Competition,
        string Season,
        string? CertificateType,
        DateOnly? ValidFrom,
        DateOnly? ValidTo,
        DateOnly IssuedOn,
        DateOnly? RevokedOn,

        /// <summary>Whether the document itself or an unresolved sanction, either one, is a reason to refuse this person entry today.</summary>
        bool EntryBlocked,
        IReadOnlyList<ActiveSanction> ActiveSanctions);

    public static IEndpointRouteBuilder MapScanCredential(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/documents/verify/{serial}", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ScanCredential))
            .WithSummary("Reads what a printed document names, and whether it or a sanction blocks entry.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        string serial,
        SportFrogDbContext database,
        ActiveSanctions activeSanctions,
        AthletePhoto photos,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSerial(serial);

        if (normalized.Length == 0)
        {
            return Results.NotFound();
        }

        // Tolerant of hyphens, spacing and case the same way the public
        // endpoint's own resolve_public_document is — a card is scanned
        // through the QR almost always, but the serial is still something a
        // steward could end up typing by hand (see Serial's own remarks).
        var documentId = await database.Database
            .SqlQuery<Guid?>(
                $"""
                 SELECT id AS "Value"
                 FROM issued_documents
                 WHERE upper(regexp_replace(serial_number, '[^A-Za-z0-9]', '', 'g')) = {normalized}
                 """)
            .SingleOrDefaultAsync(cancellationToken);

        if (documentId is not { } id)
        {
            return Results.NotFound();
        }

        var document = await database.IssuedDocuments
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                candidate.SerialNumber,
                candidate.VisibleId,
                candidate.Kind,
                candidate.Status,
                candidate.RevocationReason,
                candidate.AthleteId,
                candidate.CompetitionId,
                HolderFirstName = candidate.Athlete!.FirstName,
                HolderLastName = candidate.Athlete.LastName,
                PhotoKey = candidate.Athlete.PhotoKey,
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

            // The query above already proved the row exists.
            .SingleAsync(cancellationToken);

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var documentStatus = VerifyDocument.State(document.Status, document.ValidFrom, document.ValidTo, today);

        var sanctions = document.AthleteId is { } athleteId
            ? await activeSanctions.ForAthletesAsync(document.CompetitionId, [athleteId], cancellationToken)
            : [];

        var photoUrl = await photos.LinkAsync(document.PhotoKey, cancellationToken);

        var activeSanctionsForResponse = sanctions
            .Select(sanction => new ActiveSanction(
                sanction.SanctionId,
                sanction.Reason,
                sanction.CompletionRule,
                sanction.MatchesRequired,
                sanction.MatchesServed,
                sanction.MatchesRemaining,
                sanction.FineAmount,
                sanction.FineBalance,
                sanction.CredentialWithheld))
            .ToList();

        return Results.Ok(new Response(
            document.SerialNumber,
            document.VisibleId,
            document.Kind,
            documentStatus,
            documentStatus == VerificationState.Revoked ? document.RevocationReason : null,
            document.HolderFirstName,
            document.HolderLastName,
            photoUrl,
            document.Team,
            document.Club,
            document.Category,
            document.Competition,
            document.Season,
            document.CertificateType,
            document.ValidFrom,
            document.ValidTo,
            DateOnly.FromDateTime(document.IssuedAt.UtcDateTime),
            document.RevokedAt is { } revoked ? DateOnly.FromDateTime(revoked.UtcDateTime) : null,
            documentStatus != VerificationState.Valid || activeSanctionsForResponse.Count > 0,
            activeSanctionsForResponse));
    }

    /// <summary>
    /// The same normalization <c>resolve_public_document</c> applies to a
    /// serial before matching it — kept to the exact same rule so a card
    /// resolves identically whichever of the two endpoints reads it.
    /// </summary>
    private static string NormalizeSerial(string serial) =>
        Regex.Replace(serial, "[^A-Za-z0-9]", "").ToUpperInvariant();
}

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
/// to the person in front of them. The identity document, the date of birth,
/// the guardian's contact and the photograph itself are none of a scanner's
/// business and are not here (RNF-16) — and neither is the reason a document
/// was revoked, which is an internal matter about a person and not part of
/// the answer "this card is no longer valid".
/// </remarks>
public static class VerifyDocument
{
    public sealed record Response(
        string SerialNumber,
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

    private static async Task<IResult> HandleAsync(
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
                        candidate.Kind,
                        candidate.Status,
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

                return new Response(
                    document.SerialNumber,
                    document.Kind,
                    State(document.Status, document.ValidFrom, document.ValidTo, today),

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
    /// </remarks>
    private static VerificationState State(
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

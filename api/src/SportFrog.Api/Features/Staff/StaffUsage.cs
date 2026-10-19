using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Staff;

/// <summary>
/// Whether anything was ever issued on the strength of a staff registration.
/// </summary>
/// <remarks>
/// This is what separates the two ways of removing somebody, and it is the
/// staff counterpart of <c>Features.Rosters.RosterUsage</c>. There the
/// question is whether events were recorded; here nothing can be — a player
/// event cannot name a doctor, which is one of the reasons these are separate
/// tables — so the equivalent fact is a credential that was printed and
/// handed over. Somebody is carrying a card that says this person is this
/// team's coach, and a scan of it still has to resolve.
///
/// The database would not have stopped the strike: it is a logical delete, so
/// issued_documents keeps pointing at a person whose registration has merely
/// become invisible. The honest removal at that point is a withdrawal, which
/// says they left and keeps the record of why the card exists; revoking the
/// card itself is <c>Features.Documents.RevokeDocument</c>'s job and a
/// separate decision.
/// </remarks>
internal sealed class StaffUsage(SportFrogDbContext database)
{
    /// <summary>
    /// Whether a document was ever issued to this person for the competition
    /// this team plays in.
    /// </summary>
    /// <remarks>
    /// By person and competition rather than by staff row, because that is
    /// what a credential names: issued_documents carries an athlete and a
    /// competition, never the registration that justified it.
    /// </remarks>
    public Task<bool> HasIssuedDocumentsAsync(
        Guid athleteId,
        Guid competitionId,
        CancellationToken cancellationToken) =>
        database.IssuedDocuments
            .AsNoTracking()
            .AnyAsync(
                document => document.AthleteId == athleteId
                    && document.CompetitionId == competitionId,
                cancellationToken);
}

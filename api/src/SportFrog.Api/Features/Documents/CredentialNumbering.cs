using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Claims the next visible number for one competition's credentials, and
/// formats it with a delegation's code.
/// </summary>
/// <remarks>
/// The claim is one atomic statement — an upsert that increments and returns
/// in the same round trip — because two batches of the same competition can
/// be composing credentials at the same instant, and nothing else in this
/// pipeline stops them from handing out the same number otherwise. Reading
/// <c>next_number</c>, incrementing it in memory and writing it back would be
/// exactly the race this avoids: the database does the increment, under its
/// own row lock, not the application.
/// </remarks>
public sealed class CredentialNumbering(SportFrogDbContext database)
{
    public async Task<string> NextAsync(
        Guid organizationId,
        Guid competitionId,
        string? clubShortName,
        string clubFullName,
        CancellationToken cancellationToken)
    {
        var number = await ClaimAsync(organizationId, competitionId, cancellationToken);
        var code = VisibleCredentialId.ClubCode(clubShortName, clubFullName);

        return VisibleCredentialId.Format(code, number);
    }

    /// <summary>
    /// The number itself, leaving the row pointed at the one after it.
    /// </summary>
    /// <remarks>
    /// A plain INSERT would fail the second time this competition's counter
    /// is claimed; a plain UPDATE would do nothing the first time, because
    /// there is no row yet to update. <c>INSERT ... ON CONFLICT DO UPDATE</c>
    /// is both at once — "create it at the second number and hand out the
    /// first" when there is no row, "take the stored number and leave the one
    /// after it" when there is — in the single statement that makes it
    /// atomic.
    /// </remarks>
    private async Task<int> ClaimAsync(
        Guid organizationId, Guid competitionId, CancellationToken cancellationToken)
    {
        // Not composable: EF's SqlQuery wraps a SELECT in an outer query to
        // support further LINQ, and an INSERT ... RETURNING cannot be
        // wrapped that way. ToListAsync reads the one row this statement
        // ever returns without asking EF to compose anything over it.
        var claimed = await database.Database.SqlQuery<int>(
            $"""
            INSERT INTO credential_number_counters (competition_id, org_id, next_number)
            VALUES ({competitionId}, {organizationId}, 2)
            ON CONFLICT (competition_id)
                DO UPDATE SET next_number = credential_number_counters.next_number + 1
            RETURNING next_number - 1
            """)
            .ToListAsync(cancellationToken);

        return claimed.Single();
    }
}

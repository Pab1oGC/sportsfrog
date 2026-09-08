using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Clubs;

/// <summary>
/// The one club, per organization, that an athlete with no delegation of
/// their own enrolls under.
/// </summary>
/// <remarks>
/// <c>teams.club_id</c> stays <c>NOT NULL</c>: rather than teaching every
/// reader of <c>team.club.name</c> — credentials, the public portal,
/// listings — a second case for "no delegation", an unaffiliated athlete
/// enrolls under this club like any other.
///
/// Created lazily, the first time an organization actually needs one, rather
/// than when the organization itself is set up: most organizations run only
/// team sports and would never use it. <see cref="ReadClubs"/> excludes it
/// from listings and pickers meant for clubs a delegate manages — it is not
/// one.
/// </remarks>
internal sealed class UnaffiliatedClub(SportFrogDbContext database)
{
    private const string Name = "Sin afiliación";

    public async Task<Club> EnsureAsync(Guid orgId, CancellationToken cancellationToken)
    {
        if (await database.Clubs
                .SingleOrDefaultAsync(club => club.IsUnaffiliated, cancellationToken) is { } existing)
        {
            return existing;
        }

        var club = new Club
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = Name,
            IsUnaffiliated = true,
        };

        database.Clubs.Add(club);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two enrollments racing to create the same organization's first
            // unaffiliated club. uq_clubs_unaffiliated is the guarantee this
            // method exists to keep — whoever lost the race reads back what
            // the winner created.
            database.Entry(club).State = EntityState.Detached;

            return await database.Clubs
                .SingleAsync(candidate => candidate.IsUnaffiliated, cancellationToken);
        }

        return club;
    }
}

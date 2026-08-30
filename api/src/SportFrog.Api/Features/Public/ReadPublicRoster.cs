using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// The squad of a team, when the competition publishes squads.
/// </summary>
/// <remarks>
/// The one reading in this module that is off by default, and the reason is
/// RNF-16: these are frequently the names of children. Publishing them has to
/// be something an organization turned on deliberately, so the setting starts
/// false and a competition that never opened its settings keeps its squads
/// private while still publishing everything else.
///
/// What is published, even then, is a team sheet and nothing more: a name, a
/// shirt number, a position. The identity document, the date of birth, the
/// guardian's name and telephone, the photograph — all of them exist on the
/// athlete and none of them leave the organization. The referee checking a
/// player at the side of the pitch needs the document and the date of birth;
/// a visitor reading a web page needs neither, and one flag should not be the
/// difference between publishing a line-up and publishing a minor's identity
/// papers.
/// </remarks>
public static class ReadPublicRoster
{
    public sealed record Player(
        string FirstName,
        string LastName,
        short? JerseyNumber,
        string? Position);

    public sealed record Response(
        Guid TeamId,
        string TeamName,
        string ClubName,
        string? ClubLogoUrl,
        Guid CategoryId,
        string CategoryName,
        IReadOnlyList<Player> Players);

    public static IEndpointRouteBuilder MapReadPublicRoster(this IEndpointRouteBuilder routes)
    {
        routes.MapGet($"{PublicRoutes.Prefix}/teams/{{teamId:guid}}/roster", HandleAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicRoster))
            .WithSummary("Reads the squad of a team, where the competition publishes squads.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        string organizationSlug,
        string competitionSlug,
        Guid teamId,
        PublicCompetitionReader reader,
        ObjectStore store,
        CancellationToken cancellationToken)
    {
        var page = await reader.ReadAsync(
            organizationSlug,
            competitionSlug,
            async (database, resolved) =>
            {
                var settings = await database.Competitions
                    .AsNoTracking()
                    .Where(competition => competition.Id == resolved.CompetitionId)
                    .Select(competition => competition.Settings)
                    .SingleAsync(cancellationToken);

                // Absent settings leave squads private, which is the default
                // the record declares. Answered as not found rather than
                // forbidden, like every other refusal on this path.
                if (settings.Public?.ShowRosters is not true)
                {
                    return new Gate(null);
                }

                var team = await database.Teams
                    .AsNoTracking()

                    // Scoped to the resolved competition as well as by
                    // identifier. Without it, a team of another competition of
                    // the same organization would be readable through the
                    // address of one that publishes squads — its own setting
                    // bypassed by borrowing somebody else's.
                    .Where(candidate => candidate.Id == teamId
                        && candidate.Category!.CompetitionId == resolved.CompetitionId)
                    .Select(candidate => new
                    {
                        candidate.Id,
                        candidate.Name,
                        ClubName = candidate.Club!.Name,
                        ClubLogoKey = candidate.Club.LogoUrl,
                        candidate.CategoryId,
                        CategoryName = candidate.Category!.Name,
                    })
                    .SingleOrDefaultAsync(cancellationToken);

                if (team is null)
                {
                    return new Gate(null);
                }

                var logoUrl = string.IsNullOrEmpty(team.ClubLogoKey)
                    ? null
                    : await store.ReadLinkAsync(resolved.OrganizationId, team.ClubLogoKey, cancellationToken);

                var players = await database.RosterEntries
                    .AsNoTracking()

                    // Who is on the team now. Somebody who withdrew played
                    // for it and keeps their statistics, but a squad list is
                    // read to find out who turns out on Sunday.
                    .Where(entry => entry.TeamId == teamId && entry.WithdrawnAt == null)
                    .OrderBy(entry => entry.JerseyNumber == null)
                    .ThenBy(entry => entry.JerseyNumber)
                    .ThenBy(entry => entry.Athlete!.LastName)
                    .Select(entry => new Player(
                        entry.Athlete!.FirstName,
                        entry.Athlete.LastName,
                        entry.JerseyNumber,
                        entry.Position))
                    .ToListAsync(cancellationToken);

                return new Gate(new Response(
                    team.Id, team.Name, team.ClubName, logoUrl, team.CategoryId, team.CategoryName, players));
            },
            cancellationToken);

        return page?.Payload is null ? Results.NotFound() : Results.Ok(page.Payload);
    }

    /// <summary>
    /// Carries a refusal back through a reader whose own null means the
    /// address did not resolve. Both end as the same answer.
    /// </summary>
    private sealed record Gate(object? Payload);
}

using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// What happened in a published match, in the order it happened — the goals
/// and cards a visitor reads as the chronology of the game.
/// </summary>
/// <remarks>
/// Gated by the same setting as <see cref="ReadPublicRoster"/>, and for the
/// same reason: an event names the player it happened to, and "63' gol de
/// Juan Pérez" identifies a child exactly as precisely as a team sheet does.
/// RNF-16 does not stop mattering because the name is attached to a goal
/// instead of a shirt number — publishing one without gating it the same way
/// would reopen through a match report exactly the door the roster setting
/// exists to keep shut.
///
/// The result itself is never gated, on the calendar or here — a competition
/// that is public at all has already chosen to show who won. What is gated
/// is who is named for it.
/// </remarks>
public static class ReadPublicMatchEvents
{
    public sealed record Event(
        string FirstName,
        string LastName,
        short? JerseyNumber,

        /// <summary>
        /// Which side the roster entry that recorded this plays for. Not
        /// necessarily who it counts for — see <see cref="CountsForOpponent"/>.
        /// </summary>
        bool IsHome,
        string MetricCode,
        string MetricLabel,
        bool AffectsScore,

        /// <summary>
        /// True only for an own goal: the player named is on the side
        /// opposite <see cref="IsHome"/>, and the reader is the one who
        /// decides what that means for how the line is drawn.
        /// </summary>
        bool CountsForOpponent,
        short? PeriodNumber,
        short? Minute,
        int Quantity);

    public sealed record Response(IReadOnlyList<Event> Events);

    public static IEndpointRouteBuilder MapReadPublicMatchEvents(this IEndpointRouteBuilder routes)
    {
        routes.MapGet($"{PublicRoutes.Prefix}/matches/{{matchId:guid}}/events", HandleAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicMatchEvents))
            .WithSummary("Reads the chronology of a published match, where the competition publishes squads.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        string organizationSlug,
        string competitionSlug,
        Guid matchId,
        PublicCompetitionReader reader,
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
                // the record declares — and an event, unlike a result, names
                // somebody.
                if (settings.Public?.ShowRosters is not true)
                {
                    return new Gate(null);
                }

                // Scoped to the resolved competition as well as by
                // identifier, the same reason ReadPublicRoster scopes its
                // team: without it, a match of another competition of the
                // same organization would be readable through the address of
                // one that publishes squads.
                var match = await database.Matches
                    .AsNoTracking()
                    .Where(candidate => candidate.Id == matchId
                        && candidate.CompetitionId == resolved.CompetitionId)
                    .Select(candidate => new { candidate.HomeTeamId })
                    .SingleOrDefaultAsync(cancellationToken);

                if (match is null)
                {
                    return new Gate(null);
                }

                var events = await database.PlayerEvents
                    .AsNoTracking()
                    .Where(recorded => recorded.MatchId == matchId)

                    // The same ordering ReadEvents hands the organization:
                    // period and minute, untimed last. A chronology read out
                    // of order is not a chronology.
                    .OrderBy(recorded => recorded.PeriodNumber == null)
                    .ThenBy(recorded => recorded.PeriodNumber)
                    .ThenBy(recorded => recorded.Minute == null)
                    .ThenBy(recorded => recorded.Minute)
                    .Select(recorded => new Event(
                        recorded.RosterEntry!.Athlete!.FirstName,
                        recorded.RosterEntry.Athlete.LastName,
                        recorded.RosterEntry.JerseyNumber,
                        recorded.RosterEntry.TeamId == match.HomeTeamId,
                        recorded.Metric!.Code,
                        recorded.Metric.Label,
                        recorded.Metric.AffectsScore,
                        recorded.Metric.CountsForOpponent,
                        recorded.PeriodNumber,
                        recorded.Minute,
                        recorded.Quantity))
                    .ToListAsync(cancellationToken);

                return new Gate(new Response(events));
            },
            cancellationToken);

        // Not published, no such match, or the competition keeps squads
        // private: all answered the same way, for the reason every other
        // refusal here is — the difference is not this visitor's to learn.
        return page?.Payload is null ? Results.NotFound() : Results.Ok(page.Payload);
    }

    private sealed record Gate(object? Payload);
}

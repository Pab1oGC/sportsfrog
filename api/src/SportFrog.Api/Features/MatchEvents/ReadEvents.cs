using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.MatchEvents;

/// <summary>
/// Reads what happened in a match.
/// </summary>
public static class ReadEvents
{
    public sealed record Summary(
        Guid Id,
        Guid MatchId,
        Guid RosterEntryId,
        Guid AthleteId,
        string FirstName,
        string LastName,
        short? JerseyNumber,
        Guid TeamId,
        string TeamName,
        Guid MetricId,
        string MetricCode,
        string MetricLabel,
        bool AffectsScore,
        short? PeriodNumber,
        short? Minute,
        int Quantity);

    public static IEndpointRouteBuilder MapReadEvents(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/matches/{matchId:guid}/events", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadEvents))
            .WithSummary("Reads the events of a match.");

        routes.MapGet("/events/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadEvent")
            .WithSummary("Reads one event.");

        return routes;
    }

    /// <summary>
    /// The events of one match, in the order they happened.
    /// </summary>
    /// <remarks>
    /// The match is checked first: one that does not exist and one where
    /// nothing has been recorded yet are different answers, and both would
    /// otherwise come back as an empty list.
    ///
    /// Ordered by period and minute, with the untimed last — an event nobody
    /// wrote a minute against belongs at the end of a timeline rather than at
    /// its start.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        Guid matchId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (!await database.Matches.AnyAsync(match => match.Id == matchId, cancellationToken))
        {
            return Results.NotFound();
        }

        return Results.Ok(await Project(database.PlayerEvents
                .Where(recorded => recorded.MatchId == matchId)
                .OrderBy(recorded => recorded.PeriodNumber == null)
                .ThenBy(recorded => recorded.PeriodNumber)
                .ThenBy(recorded => recorded.Minute == null)
                .ThenBy(recorded => recorded.Minute))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var recorded = await Project(database.PlayerEvents.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return recorded is null ? Results.NotFound() : Results.Ok(recorded);
    }

    /// <summary>
    /// Shared so every reading of an event describes it the same way.
    /// </summary>
    /// <remarks>
    /// The player, the shirt, the team and the label all travel with it,
    /// because the thing being rendered is a line of a match report — "12
    /// Soto, gol, 63'" — and resolving four identifiers per line would be
    /// four requests per goal.
    ///
    /// Whether the metric moves the score comes along too: a client showing a
    /// timeline separates the goals from the cards, and that distinction is
    /// the catalog's answer rather than a list of codes each client keeps.
    /// </remarks>
    private static IQueryable<Summary> Project(IQueryable<PlayerEvent> events) =>
        events.Select(recorded => new Summary(
            recorded.Id,
            recorded.MatchId,
            recorded.RosterEntryId,
            recorded.RosterEntry!.AthleteId,
            recorded.RosterEntry.Athlete!.FirstName,
            recorded.RosterEntry.Athlete.LastName,
            recorded.RosterEntry.JerseyNumber,
            recorded.RosterEntry.TeamId,
            recorded.RosterEntry.Team!.Name,
            recorded.MetricId,
            recorded.Metric!.Code,
            recorded.Metric.Label,
            recorded.Metric.AffectsScore,
            recorded.PeriodNumber,
            recorded.Minute,
            recorded.Quantity));
}

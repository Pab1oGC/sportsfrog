using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// When and where a published competition is played, and how it ended.
/// </summary>
/// <remarks>
/// Not behind a setting, unlike the table and the boards. Publishing a
/// competition is mostly an act of publishing this: a parent looking up what
/// time the match is on Sunday is the reason the public page exists, and a
/// competition that wanted to keep its fixtures private would not be
/// published at all.
///
/// It is deliberately not the same projection the organization reads. That
/// one carries the notes a referee or an organizer wrote on the fixture,
/// which are working notes about people and belong inside the organization.
/// Sharing the projection to save a few lines would have published them by
/// accident the first time somebody added a field.
/// </remarks>
public static class ReadPublicCalendar
{
    public sealed record Fixture(
        Guid Id,
        Guid CategoryId,
        string CategoryName,
        string HomeTeamName,
        string AwayTeamName,
        string? VenueName,
        string? SpaceName,
        DateTimeOffset? ScheduledAt,
        short? RoundNumber,
        string? Phase,
        MatchState Status,
        int? HomeTotal,
        int? AwayTotal);

    public sealed record Response(IReadOnlyList<Fixture> Fixtures);

    public static IEndpointRouteBuilder MapReadPublicCalendar(this IEndpointRouteBuilder routes)
    {
        routes.MapGet($"{PublicRoutes.Prefix}/matches", HandleAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicCalendar))
            .WithSummary("Reads the calendar of a published competition.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        string organizationSlug,
        string competitionSlug,
        PublicCompetitionReader reader,
        CancellationToken cancellationToken,
        Guid? categoryId = null,
        string? status = null,
        short? round = null)
    {
        // A query parameter reaches no validator. An unreadable state is
        // ignored rather than refused: a visitor followed a link, and the
        // useful answer to a mistyped filter is the calendar rather than a
        // problem document.
        var state = WireEnum.TryParse<MatchState>(QueryFilter.OrAbsent(status), out var parsed)
            ? parsed
            : (MatchState?)null;

        var page = await reader.ReadAsync(
            organizationSlug,
            competitionSlug,
            async (database, resolved) => new Response(
                await database.Matches
                    .AsNoTracking()
                    .Where(match => match.CompetitionId == resolved.CompetitionId)
                    .Where(match => categoryId == null || match.CategoryId == categoryId)
                    .Where(match => state == null || match.Status == state)
                    .Where(match => round == null || match.RoundNumber == round)

                    // Chronological with the undated last, which is where a
                    // fixture drawn but not yet placed belongs on a page
                    // somebody is reading to find out when they play.
                    .OrderBy(match => match.ScheduledAt == null)
                    .ThenBy(match => match.ScheduledAt)
                    .ThenBy(match => match.RoundNumber)
                    .Select(match => new Fixture(
                        match.Id,
                        match.CategoryId,
                        match.Category!.Name,
                        match.HomeTeam!.Name,
                        match.AwayTeam!.Name,
                        match.VenueSpace!.Venue!.Name,
                        match.VenueSpace.Name,
                        match.ScheduledAt,
                        match.RoundNumber,
                        match.Phase,
                        match.Status,
                        match.HomeTotal,
                        match.AwayTotal))
                    .ToListAsync(cancellationToken)),
            cancellationToken);

        return page is null ? Results.NotFound() : Results.Ok(page);
    }
}

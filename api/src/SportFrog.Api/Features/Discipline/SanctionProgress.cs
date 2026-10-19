using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// How many of a sanctioned team's matches count toward clearing a
/// suspension counted in matches.
/// </summary>
/// <remarks>
/// A match counts once it has a result that stands — finished, or awarded by
/// walkover, the same two <c>MatchState</c> values a standings table counts
/// — and once it was scheduled on or after the sanction was imposed. Scheduled
/// date, not when the result happened to be typed in: a suspension is "sit
/// out the next N fixtures", a question about which match it was on the
/// calendar, not about when an operator got around to recording it. A match
/// with no date yet does not count either way — there is nothing yet to
/// compare against <c>ImposedAt</c>.
///
/// Batched across every sanction asked about in one call, the same shape
/// <c>AccreditationResolver.ResolveManyAsync</c> takes for the same reason:
/// a competition's sanctions list reads this once, not once per row.
/// </remarks>
internal sealed class SanctionProgress(SportFrogDbContext database)
{
    public async Task<IReadOnlyDictionary<Guid, int>> MatchesServedAsync(
        Guid competitionId,
        IReadOnlyCollection<Sanction> sanctions,
        CancellationToken cancellationToken)
    {
        if (sanctions.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var teamIds = sanctions.Select(sanction => sanction.TeamId).Distinct().ToList();

        var played = await database.Matches
            .AsNoTracking()
            .Where(match => match.CompetitionId == competitionId)
            .Where(match => match.Status == MatchState.Finished || match.Status == MatchState.Walkover)
            .Where(match => match.ScheduledAt != null)
            .Where(match =>
                (match.HomeTeamId != null && teamIds.Contains(match.HomeTeamId.Value))
                || (match.AwayTeamId != null && teamIds.Contains(match.AwayTeamId.Value)))
            .Select(match => new { match.HomeTeamId, match.AwayTeamId, match.ScheduledAt })
            .ToListAsync(cancellationToken);

        return sanctions.ToDictionary(
            sanction => sanction.Id,
            sanction => played.Count(match =>
                (match.HomeTeamId == sanction.TeamId || match.AwayTeamId == sanction.TeamId)
                && match.ScheduledAt >= sanction.ImposedAt));
    }
}

using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Turns a knockout drawn in full — <see cref="Bracket.FullDraw"/> — into the
/// rows that hold it.
/// </summary>
/// <remarks>
/// Shared by everything that draws a bracket to the final in one go: a pure
/// knockout at <see cref="DrawCalendar"/>, and a group stage's knockout at
/// <see cref="PromoteGroupStage"/>. Both need exactly the same thing, and the
/// part worth not writing twice is the ids: a later round's
/// <c>HomeSourceMatchId</c>/<c>AwaySourceMatchId</c> point at an earlier
/// round's row, so every id has to exist before any row is built.
/// </remarks>
internal static class PlannedBracket
{
    public static List<Match> ToMatches(
        IReadOnlyList<PlannedMatch> plan,
        Guid organizationId,
        Guid competitionId,
        Guid categoryId)
    {
        // Assigned up front so a later round's source ids can point at an
        // earlier one's id before any of them exist as a row — nothing else
        // needs these to be stable before SaveChangesAsync.
        var ids = plan.Select(_ => Guid.NewGuid()).ToList();

        return [.. plan.Select((planned, index) => new Match
        {
            Id = ids[index],
            OrgId = organizationId,
            CompetitionId = competitionId,
            CategoryId = categoryId,
            HomeTeamId = planned.Home.TeamId,
            AwayTeamId = planned.Away.TeamId,
            HomeSourceMatchId = planned.Home.SourceMatchIndex is { } homeSource ? ids[homeSource] : null,
            AwaySourceMatchId = planned.Away.SourceMatchIndex is { } awaySource ? ids[awaySource] : null,
            RoundNumber = (short)planned.Round,
            Phase = planned.Phase,
            Status = MatchState.Scheduled,
        })];
    }
}

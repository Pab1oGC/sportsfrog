using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Matches;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Copies a decided match's winner into whichever later match was waiting on
/// it, for a knockout drawn in full ahead of being played.
/// </summary>
/// <remarks>
/// The counterpart, on the other side of the same idea, to
/// <see cref="Draw.AdvanceBracket"/> — that one still exists, for a bracket
/// promoted from a group stage or one drawn before this file did, and draws
/// a whole new round once every match of the last one is decided. This is
/// for a bracket that already has every round's fixture, date and venue
/// booked: nothing new is drawn here, a placeholder already on the calendar
/// is just filled in.
///
/// Called from every match that can decide a knockout tie, right before it
/// saves — finishing one, awarding one, and breaking one's tie by shootout —
/// because each of those can be the moment <see cref="MatchWinner.Resolve"/>
/// starts answering something it did not before. Left to the caller's own
/// <c>SaveChangesAsync</c> to persist, so the decided match and whatever it
/// unlocks commit together in one round trip.
/// </remarks>
internal static class BracketWinnerPropagation
{
    public static async Task ApplyAsync(
        Match decided,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (decided.Phase is null)
        {
            // Never a source match for anything: only a knockout drawn in
            // full ever sets home_source_match_id/away_source_match_id, and
            // every match it draws carries a phase.
            return;
        }

        if (MatchWinner.Resolve(
                decided.HomeTeamId!.Value,
                decided.AwayTeamId!.Value,
                decided.WalkoverTeamId,
                decided.HomeTotal,
                decided.AwayTotal,
                decided.PenaltyHomeScore,
                decided.PenaltyAwayScore) is not { } winner)
        {
            return;
        }

        var dependents = await database.Matches
            .Where(match => match.HomeSourceMatchId == decided.Id || match.AwaySourceMatchId == decided.Id)
            .ToListAsync(cancellationToken);

        foreach (var dependent in dependents)
        {
            if (dependent.HomeSourceMatchId == decided.Id)
            {
                dependent.HomeTeamId = winner;
            }

            if (dependent.AwaySourceMatchId == decided.Id)
            {
                dependent.AwayTeamId = winner;
            }
        }
    }
}

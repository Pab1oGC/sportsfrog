using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.MatchEvents;

/// <summary>
/// Keeps an already-finished match's score lined up with its own events.
/// </summary>
/// <remarks>
/// <see cref="RecordResult.HandleAsync"/> and
/// <see cref="RecordResult.HandleFromEventsAsync"/> both decide a match's
/// score once, at the moment somebody says it is over —
/// <see cref="RecordResult"/>'s own cross-check
/// (<c>RefuseIfContradictsEventsAsync</c>) makes sure that moment's events
/// and score agree. Nothing kept that agreement current afterwards:
/// correcting a goal's player, or deleting one logged against the wrong
/// period, left the score exactly as it was finished with — event and
/// result silently disagreeing from then on, the same inconsistency this
/// whole feature exists to close, just reached by editing the event
/// instead of the result.
///
/// Deliberately best-effort and silent, never another reason an event
/// edit can be refused: fixing who scored a goal should not fail because
/// of something unrelated about the match's score. Every condition below
/// that is not obviously safe to resync — nothing here ever affects the
/// score to begin with (wally, a judged sport), the recorded events cannot
/// honestly be read into one right now (data from before
/// <see cref="EventPolicy.InspectPeriod"/> required a period under
/// <see cref="SportFrog.Domain.Rules.ScoreMode.Sets"/>, or a deciding set
/// not yet reached), or the winner this match decided already sent someone
/// into a later match that has itself started — leaves the match's score
/// exactly as it was and lets the event change proceed anyway. A stale
/// score is the one problem this already had; it is never a new one this
/// creates.
/// </remarks>
internal sealed class EventDrivenResult(
    MatchRulesLookup rulesLookup,
    ResultPolicy policy,
    IMatchOutcomeRulesRegistry outcomeRules,
    OrganizationContext organization,
    TimeProvider clock)
{
    /// <summary>
    /// Re-derives and re-applies <paramref name="matchId"/>'s score from its
    /// events, if it is finished and there is anything safe to re-derive.
    /// </summary>
    /// <remarks>
    /// Called after the event change it is reacting to has already been
    /// saved — <see cref="MatchRulesLookup"/> and the events query both read
    /// the database as it now stands, so this always sees the edit that
    /// triggered it.
    /// </remarks>
    public async Task ResyncAsync(Guid matchId, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        var match = await database.Matches.SingleOrDefaultAsync(
            candidate => candidate.Id == matchId, cancellationToken);

        if (match is not { Status: MatchState.Finished })
        {
            // Nothing closed yet to keep honest, or no such match (a race
            // with the same event's own delete finding it first) — either
            // way, there is no result here for the events to disagree with.
            return;
        }

        if (await rulesLookup.FindRulesAsync(match, cancellationToken) is not { } rules)
        {
            return;
        }

        var (periods, hasEvents, refusal) =
            await RecordResult.TryDerivePeriodsFromEventsAsync(match, rules, database, cancellationToken);

        if (refusal is not null || !hasEvents || periods is null)
        {
            // Nothing to sum here (wally, a judged sport), or deleting the
            // event that triggered this left none behind at all — the
            // match keeps whatever score it was finished with, the same as
            // before this existed.
            return;
        }

        if (policy.Inspect(rules, periods).Count > 0)
        {
            // The events, summed honestly, do not add up to a finished
            // match right now — a deciding set not yet reached, say.
            // Forcing that into the score would be the exact silent
            // mismatch this exists to prevent, read backwards.
            return;
        }

        if (RecordResult.RefuseUnpriceableTie(match, rules, periods) is not null)
        {
            // Same tie RecordResult.HandleFromEventsAsync would refuse to
            // finish outright: nothing here prices it and nothing here can
            // break it. Applying it quietly would just move the same
            // problem from a request somebody would have seen fail to a
            // background resync nobody is watching.
            return;
        }

        var (home, away) = outcomeRules.For(rules.Sport.ScoreMode).Consolidate(periods);

        if (await WouldOrphanAnAdvancedMatchAsync(match, home, away, database, cancellationToken))
        {
            // This match named who played a later one, that later match has
            // already started or finished with the team this used to
            // resolve to, and the re-derived score now names someone else.
            // Applying it here would quietly change who was supposed to
            // have played the later match, which is a decision for whoever
            // runs the bracket to make on purpose, not a side effect of
            // correcting one event.
            return;
        }

        try
        {
            await RecordResult.ApplyResultAsync(
                match, home, away, periods, notes: null, database, organization, clock, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody else corrected this same match's result in the
            // moment between reading it here and saving the resync —
            // their save is the current truth, and the next event change
            // against this match will try resyncing again if it still
            // disagrees with it.
        }
    }

    /// <summary>
    /// Whether re-deriving this match's score to <paramref name="homeTotal"/>
    /// / <paramref name="awayTotal"/> would change who it sent into a later
    /// match that has already started or finished with the team this
    /// decided before.
    /// </summary>
    private static async Task<bool> WouldOrphanAnAdvancedMatchAsync(
        Match match,
        int homeTotal,
        int awayTotal,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (match.Phase is null)
        {
            // Never a source match for anything — see
            // BracketWinnerPropagation's own remark: only a knockout drawn
            // in full ever sets home_source_match_id/away_source_match_id,
            // and every match it draws carries a phase.
            return false;
        }

        var previousWinner = MatchWinner.Resolve(
            match.HomeTeamId!.Value, match.AwayTeamId!.Value, match.WalkoverTeamId,
            match.HomeTotal, match.AwayTotal, match.PenaltyHomeScore, match.PenaltyAwayScore);

        // ApplyResultAsync always clears a penalty shootout on a corrected
        // score (its own remark says why), so the outcome this resync is
        // about to decide never has one to break a tie with either.
        var nextWinner = MatchWinner.Resolve(
            match.HomeTeamId!.Value, match.AwayTeamId!.Value, match.WalkoverTeamId,
            homeTotal, awayTotal, penaltyHomeScore: null, penaltyAwayScore: null);

        if (previousWinner == nextWinner)
        {
            return false;
        }

        return await database.Matches
            .Where(dependent => dependent.HomeSourceMatchId == match.Id || dependent.AwaySourceMatchId == match.Id)
            .AnyAsync(
                dependent => dependent.Status == MatchState.InProgress
                    || dependent.Status == MatchState.Finished
                    || dependent.Status == MatchState.Walkover,
                cancellationToken);
    }
}

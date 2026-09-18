-- =============================================================================
-- AddTaekwondoKyorugiScoringEvents
--
-- AddTaekwondoCatalogFoundations seeded kyorugi's point/gam-jeom metrics as
-- pure statistics (affects_score = false), on the general rule that a
-- ScoreMode.Sets sport's result comes from periods won, never summed from
-- events. True for volleyball and wally, whose events really are nothing
-- but a leaderboard entry -- not true for kyorugi, where the point (and the
-- opponent's gam-jeom) is exactly what decides who wins the asalto being
-- fought right now, recorded live the same way a goal already is.
--
-- point.affects_score -> true. score_points stays 1: the techniques this
-- catalog does not distinguish (punch, body kick, head kick, ...) are worth
-- different amounts under WT rules, and the recorder prices that at the
-- moment they log it via quantity (already generic: "this happened, this
-- many times" -- here read as "this event, worth this many points"), not
-- by adding a metric per technique.
--
-- penalty.affects_score -> true and counts_for_opponent -> true: a gam-jeom
-- against a competitor is worth one point to the other side, the same
-- CountsForOpponent mechanism an own goal already uses to credit the score
-- to whoever the roster entry recording it does not play for.
--
-- Nothing here changes what a match still decides by: SetsMatchOutcomeRules
-- keeps counting asaltos won, not points landed. See
-- RecordResult.HandleFromEventsAsync and LiveScore.ComputePlayedPeriods for
-- the code-side half of this change.
-- =============================================================================

UPDATE sport_metrics
   SET affects_score = true
 WHERE sport_code = 'taekwondo_kyorugi' AND code = 'point';

UPDATE sport_metrics
   SET affects_score = true, counts_for_opponent = true
 WHERE sport_code = 'taekwondo_kyorugi' AND code = 'penalty';

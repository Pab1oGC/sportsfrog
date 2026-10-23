-- =============================================================================
-- AddVolleyballScoringMetrics
--
-- Volleyball's score comes from sets won (ScoreMode.Sets), consolidated from
-- period_scores rather than summed from events -- true for every metric this
-- sport seeded until now. That stops being true here, the same way
-- AddTaekwondoKyorugiScoringEvents already made it stop being true for
-- kyorugi: a rally decides the point being played right now, and recording
-- it live should be able to finish a set the same way a goal already
-- finishes a cumulative match (RecordResult.HandleFromEventsAsync,
-- LiveScore.ComputePlayedPeriods). Nothing here changes what a match is
-- decided by: SetsMatchOutcomeRules keeps counting sets won, not points
-- landed.
--
-- point, ace and the new block_point all win the rally outright and are
-- each worth one point (score_points keeps its default of 1). error is the
-- mirror of football's own_goal: recorded against the side that made the
-- mistake, worth a point to whoever they are playing
-- (counts_for_opponent = true), and never credited to a player's own board
-- (is_rankable = false) for the same reason an own goal is not either.
--
-- block is split rather than flipped outright: a touched block does not
-- always win the rally -- a deflection the point is still played out from --
-- so it cannot be assumed to score the way an ace can. It is renamed to
-- block_touch and kept exactly as it was: a pure, non-scoring statistic.
-- block_point is added alongside it as its own event, for the block that
-- does win the point outright. Renamed rather than deleted: an event
-- already recorded as "block" keeps its id and its (already non-scoring)
-- meaning unchanged, so nothing this migration touches is reinterpreted.
--
-- A volleyball ruleset that already names "block" in its own metrics
-- allow-list (RulesetConfiguration.Metrics) is rewritten below to name both
-- halves of the split instead, so a competition that opted into blocks
-- keeps seeing both rather than silently losing the one its config no
-- longer matches.
-- =============================================================================

UPDATE sport_metrics
   SET affects_score = true
 WHERE sport_code = 'volleyball' AND code IN ('point', 'ace');

UPDATE sport_metrics
   SET code = 'block_touch', label = 'Toque de bloqueo'
 WHERE sport_code = 'volleyball' AND code = 'block';

INSERT INTO sport_metrics (sport_code, code, label, affects_score, is_rankable, display_order)
VALUES ('volleyball', 'block_point', 'Bloqueo (punto)', true, true, 3);

-- Closes the gap the insert above opened and keeps display_order
-- contiguous: point 1, ace 2, block_point 3, block_touch 4, error 5.
UPDATE sport_metrics SET display_order = 4
 WHERE sport_code = 'volleyball' AND code = 'block_touch';

UPDATE sport_metrics
   SET affects_score = true, counts_for_opponent = true, is_rankable = false, display_order = 5
 WHERE sport_code = 'volleyball' AND code = 'error';

-- rulesets carries FORCE ROW LEVEL SECURITY, and the role that applies
-- migrations is not exempt from it (same dance RemoveAssistFromFootballAndFutsal
-- already uses): with no organization set, this UPDATE would match no rows
-- at all and report success. Forcing is lifted for the one statement and
-- put straight back; the whole migration is one transaction, so nothing can
-- observe the table unprotected.
ALTER TABLE rulesets NO FORCE ROW LEVEL SECURITY;

UPDATE rulesets
   SET config = jsonb_set(
         config,
         '{metrics}',
         ((config -> 'metrics') - 'block') || '["block_point", "block_touch"]'::jsonb
       )
 WHERE sport_code = 'volleyball'
   AND jsonb_typeof(config -> 'metrics') = 'array'
   AND config -> 'metrics' ? 'block';

ALTER TABLE rulesets FORCE ROW LEVEL SECURITY;

-- =============================================================================
-- AddMetricScoring
--
-- What recording a metric is worth toward the match score, and who it counts
-- for. AffectsScore alone answers "does this move the score", which was
-- enough to keep an assist off the scoreboard but not enough to draw one: a
-- three-pointer and a free throw both affect the score, and by different
-- amounts, and an own goal affects the score of the team that did NOT record
-- it.
--
-- This is what a live score — read while a match is still in progress, from
-- the events recorded so far, before anyone writes down the final result —
-- needs and the schema did not carry.
-- =============================================================================

ALTER TABLE sport_metrics
    ADD COLUMN score_points integer NOT NULL DEFAULT 1,
    ADD COLUMN counts_for_opponent boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN sport_metrics.score_points IS
  'How many points recording this metric once (quantity 1) adds to the match
   score. Meaningless where affects_score is false — nothing reads it there.';

COMMENT ON COLUMN sport_metrics.counts_for_opponent IS
  'True only for an own goal: the points it is worth go to the other team,
   not to the team the roster entry belongs to.';

-- Basketball's three scoring metrics are not worth the same amount. Every
-- other affects_score metric across every sport (a football or futsal goal,
-- a basketball free throw) is worth the default of one.
UPDATE sport_metrics SET score_points = 2
    WHERE sport_code = 'basketball' AND code = 'field_goal';

UPDATE sport_metrics SET score_points = 3
    WHERE sport_code = 'basketball' AND code = 'three_point';

-- An own goal is scored against the team it is recorded for.
UPDATE sport_metrics SET counts_for_opponent = true
    WHERE code = 'own_goal';

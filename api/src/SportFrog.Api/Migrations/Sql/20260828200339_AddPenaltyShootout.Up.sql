-- =============================================================================
-- AddPenaltyShootout
--
-- A knockout match that ends level owes the bracket a winner, and a penalty
-- shootout is how the sports this app knows settle that without touching the
-- match it was won or lost after. Both columns land beside home_total and
-- away_total rather than inside them: the 90 minutes stayed a draw, the
-- shootout is a separate fact about how the tie was broken, and a table that
-- reads goals scored must not learn to read the shootout as more of them.
--
-- Null is the ordinary state — most knockout matches, and every group match,
-- never needed one. Set only through the penalties endpoint, and only once
-- the constraints below allow it.
-- =============================================================================

ALTER TABLE matches ADD COLUMN penalty_home_score smallint;
ALTER TABLE matches ADD COLUMN penalty_away_score smallint;

COMMENT ON COLUMN matches.penalty_home_score IS
  'The home side''s shootout score, once a level knockout match needed one to
   produce a winner. Null for every match that did not.';
COMMENT ON COLUMN matches.penalty_away_score IS
  'The away side''s shootout score. See penalty_home_score.';

ALTER TABLE matches ADD CONSTRAINT ck_penalties_paired CHECK (
    (penalty_home_score IS NULL) = (penalty_away_score IS NULL));

-- A shootout is only ever recorded against a match that already has a real
-- result — you cannot break the tie of a match that has not been played —
-- and that result has to actually be level, or there is nothing to break.
ALTER TABLE matches ADD CONSTRAINT ck_penalties_only_when_finished CHECK (
    penalty_home_score IS NULL OR (status = 'finished' AND home_total = away_total));

-- A shootout that itself ends level decided nothing.
ALTER TABLE matches ADD CONSTRAINT ck_penalties_decisive CHECK (
    penalty_home_score IS NULL OR penalty_home_score <> penalty_away_score);

ALTER TABLE matches ADD CONSTRAINT ck_penalties_nonnegative CHECK (
    (penalty_home_score IS NULL OR penalty_home_score >= 0) AND
    (penalty_away_score IS NULL OR penalty_away_score >= 0));

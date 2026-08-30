-- =============================================================================
-- AddPenaltyShootout - rollback
-- =============================================================================

ALTER TABLE matches DROP CONSTRAINT ck_penalties_nonnegative;
ALTER TABLE matches DROP CONSTRAINT ck_penalties_decisive;
ALTER TABLE matches DROP CONSTRAINT ck_penalties_only_when_finished;
ALTER TABLE matches DROP CONSTRAINT ck_penalties_paired;
ALTER TABLE matches DROP COLUMN penalty_away_score;
ALTER TABLE matches DROP COLUMN penalty_home_score;

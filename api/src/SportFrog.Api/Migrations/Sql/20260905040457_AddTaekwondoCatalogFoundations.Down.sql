-- =============================================================================
-- AddTaekwondoCatalogFoundations — rollback
-- =============================================================================

DELETE FROM sport_metrics WHERE sport_code = 'taekwondo_kyorugi';
DELETE FROM sports WHERE code = 'taekwondo_kyorugi';

DROP INDEX uq_clubs_unaffiliated;
ALTER TABLE clubs DROP COLUMN is_unaffiliated;

ALTER TABLE athletes DROP CONSTRAINT ck_athletes_weight_positive;
ALTER TABLE athletes DROP COLUMN weight_kg;

ALTER TABLE categories DROP CONSTRAINT ck_categories_weight_range_valid;
ALTER TABLE categories DROP CONSTRAINT ck_categories_max_weight_positive;
ALTER TABLE categories DROP CONSTRAINT ck_categories_min_weight_positive;
ALTER TABLE categories DROP COLUMN max_weight_kg;
ALTER TABLE categories DROP COLUMN min_weight_kg;

ALTER TABLE sports DROP CONSTRAINT ck_sports_score_mode;
ALTER TABLE sports ADD CONSTRAINT sports_score_mode_check CHECK (score_mode IN ('cumulative', 'sets'));

ALTER TABLE sports DROP COLUMN is_individual;

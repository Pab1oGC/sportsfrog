-- =============================================================================
-- AddSportPeriodHasClock — rollback
-- =============================================================================

UPDATE sports SET default_minutes = NULL WHERE code = 'taekwondo_kyorugi';

ALTER TABLE sports DROP COLUMN period_has_clock;

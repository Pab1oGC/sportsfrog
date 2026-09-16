-- =============================================================================
-- AddSportDefaultPeriodMinutes — rollback
-- =============================================================================

ALTER TABLE sports DROP COLUMN default_minutes;

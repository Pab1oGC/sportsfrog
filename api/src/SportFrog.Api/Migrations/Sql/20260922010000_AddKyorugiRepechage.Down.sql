-- =============================================================================
-- AddKyorugiRepechage - rollback
-- =============================================================================

ALTER TABLE matches DROP COLUMN is_repechage;
ALTER TABLE categories DROP COLUMN uses_repechage;

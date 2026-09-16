-- =============================================================================
-- AddSportEntrySize — rollback
-- =============================================================================

ALTER TABLE sports DROP CONSTRAINT ck_sports_max_entry_size_positive;

ALTER TABLE sports DROP COLUMN max_entry_size;

-- =============================================================================
-- AddSanctions - rollback
--
-- Drops the table and its enum whole. Nothing outside this migration reads
-- or writes either.
-- =============================================================================

DROP TRIGGER IF EXISTS trg_sanctions_touch ON sanctions;

DROP TABLE IF EXISTS sanctions;

DROP TYPE IF EXISTS sanction_completion_rule;

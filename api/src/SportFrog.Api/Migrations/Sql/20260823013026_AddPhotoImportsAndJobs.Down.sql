-- =============================================================================
-- AddPhotoImportsAndJobs - rollback
--
-- The Hangfire schema is dropped with everything in it, because everything in
-- it belongs to Hangfire and was created by it. A queue is not data anybody
-- restores; work that was in flight when a deployment was rolled back is work
-- that has to be asked for again.
--
-- The uploaded archives stay in object storage. Dropping a table is not a
-- reason to destroy files somebody sent, and a re-applied migration would
-- otherwise come back to rows pointing at nothing.
-- =============================================================================

DROP SCHEMA IF EXISTS hangfire CASCADE;

DROP TRIGGER IF EXISTS trg_photo_imports_touch ON photo_imports;

DROP TABLE IF EXISTS photo_imports;

DROP TYPE IF EXISTS photo_import_state;

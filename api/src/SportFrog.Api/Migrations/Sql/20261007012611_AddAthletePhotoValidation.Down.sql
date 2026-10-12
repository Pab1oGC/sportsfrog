-- =============================================================================
-- AddAthletePhotoValidation - rollback
--
-- Undoes the .Up.sql file in reverse order: columns first, because the type
-- they use can't be dropped while they still exist.
--
-- Dropping the columns discards every verdict recorded so far. That is the
-- price of the rollback and it is intended: the rollback exists to undo a
-- failed deployment, and the verdicts can be recomputed from the photographs.
-- =============================================================================

ALTER TABLE athletes
    DROP COLUMN photo_validated_at,
    DROP COLUMN photo_rules_version,
    DROP COLUMN photo_validation_warnings,
    DROP COLUMN photo_validation_reasons,
    DROP COLUMN photo_validation_state;

DROP TYPE photo_validation_state;

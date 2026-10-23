-- =============================================================================
-- AddSetTargetPoints — rollback
-- =============================================================================

ALTER TABLE sports
    DROP COLUMN target_points_per_set,
    DROP COLUMN target_points_deciding_set;

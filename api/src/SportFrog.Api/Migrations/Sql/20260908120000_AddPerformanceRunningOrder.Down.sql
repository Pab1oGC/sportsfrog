-- =============================================================================
-- AddPerformanceRunningOrder — rollback
-- =============================================================================

DROP INDEX uq_performance_running_order;

ALTER TABLE performances
    DROP CONSTRAINT ck_performance_order_positive;

ALTER TABLE performances
    DROP COLUMN venue_space_id,
    DROP COLUMN scheduled_on,
    DROP COLUMN order_number;

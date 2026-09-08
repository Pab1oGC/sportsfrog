-- =============================================================================
-- AddPerformances — rollback
-- =============================================================================

DROP TRIGGER trg_performances_touch ON performances;

REVOKE SELECT, INSERT, UPDATE, DELETE ON performances FROM sportfrog_app;

DROP POLICY tenant_isolation ON performances;

ALTER TABLE performances DISABLE ROW LEVEL SECURITY;

DROP TABLE performances;

DROP TYPE performance_status;

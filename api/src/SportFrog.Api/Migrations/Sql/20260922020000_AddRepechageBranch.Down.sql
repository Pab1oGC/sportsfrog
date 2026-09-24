-- =============================================================================
-- AddRepechageBranch - rollback
-- =============================================================================

ALTER TABLE matches DROP CONSTRAINT ck_repechage_branch_only_on_repechage;
ALTER TABLE matches DROP CONSTRAINT ck_repechage_branch_values;
ALTER TABLE matches DROP COLUMN repechage_branch;

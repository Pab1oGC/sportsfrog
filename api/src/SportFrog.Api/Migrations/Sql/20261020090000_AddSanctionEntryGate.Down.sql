-- =============================================================================
-- AddSanctionEntryGate - rollback
-- =============================================================================

DROP FUNCTION IF EXISTS sanction_blocks_entry(uuid, uuid);

ALTER POLICY tenant_isolation ON sanctions TO sportfrog_app;
ALTER POLICY tenant_isolation ON sanction_payments TO sportfrog_app;

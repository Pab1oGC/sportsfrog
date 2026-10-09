-- =============================================================================
-- AddMembershipScopes - rollback
--
-- Drops the table and its enum whole. Nothing outside this migration yet
-- reads or writes membership_scopes, so there is no data elsewhere that
-- depends on it surviving a rollback.
-- =============================================================================

DROP TRIGGER IF EXISTS trg_membership_scopes_touch ON membership_scopes;

DROP TABLE IF EXISTS membership_scopes;

DROP TYPE IF EXISTS membership_scope_type;

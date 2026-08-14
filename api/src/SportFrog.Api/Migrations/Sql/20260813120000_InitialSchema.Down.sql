-- =============================================================================
-- SPORTFROG — Initial schema rollback
--
-- Tables are dropped in the reverse order of their dependencies. Triggers,
-- policies, indexes and constraints fall along with their table, so only
-- functions, types and extensions are dropped explicitly.
--
-- Application roles are NOT dropped: they're provisioned by the environment
-- (the container's initialization script) and may own other objects or be in
-- use by active connections. The grants this migration issued are revoked.
-- =============================================================================

DROP TRIGGER IF EXISTS trg_audit_immutable ON audit_log;

REVOKE ALL ON ALL TABLES IN SCHEMA public FROM sportfrog_app;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM sportfrog_app;
REVOKE USAGE ON SCHEMA public FROM sportfrog_app;

REVOKE ALL ON ALL TABLES IN SCHEMA public FROM sportfrog_public;
REVOKE USAGE ON SCHEMA public FROM sportfrog_public;

DROP FUNCTION IF EXISTS resolve_public_competition(citext, citext);

DROP TABLE IF EXISTS audit_log;
DROP TABLE IF EXISTS issued_documents;
DROP TABLE IF EXISTS document_templates;
DROP TABLE IF EXISTS player_events;
DROP TABLE IF EXISTS matches;
DROP TABLE IF EXISTS roster_entries;
DROP TABLE IF EXISTS teams;
DROP TABLE IF EXISTS venue_spaces;
DROP TABLE IF EXISTS venues;
DROP TABLE IF EXISTS categories;
DROP TABLE IF EXISTS competitions;
DROP TABLE IF EXISTS athletes;
DROP TABLE IF EXISTS clubs;
DROP TABLE IF EXISTS rulesets;
DROP TABLE IF EXISTS sport_metrics;
DROP TABLE IF EXISTS sports;
DROP TABLE IF EXISTS refresh_tokens;
DROP TABLE IF EXISTS organization_memberships;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS organizations;

DROP FUNCTION IF EXISTS reject_audit_mutation();
DROP FUNCTION IF EXISTS touch_updated_at();
DROP FUNCTION IF EXISTS current_org_id();

DROP TYPE IF EXISTS document_state;
DROP TYPE IF EXISTS document_kind;
DROP TYPE IF EXISTS capture_level;
DROP TYPE IF EXISTS match_state;
DROP TYPE IF EXISTS competition_state;
DROP TYPE IF EXISTS membership_role;

DROP EXTENSION IF EXISTS citext;
DROP EXTENSION IF EXISTS pgcrypto;

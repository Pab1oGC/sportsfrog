-- =============================================================================
-- AddCurrentUserContext
--
-- Signing in has to answer "which organizations does this person belong to",
-- and that answer lives in organization_memberships — a table whose only
-- policy compares org_id against the established organization. At sign-in
-- there is no established organization yet: it is precisely what is being
-- looked up. The application user therefore saw no memberships at all and
-- could never build a token.
--
-- The isolation context gains a second dimension: besides which organization
-- the transaction acts in, it can now say which person it acts as. A
-- permissive policy lets a row through when it is *about* that person.
--
-- Seeing the rows that name you is not a leak between organizations, which is
-- what the isolation exists to prevent. It is your own membership.
--
-- FOR SELECT only, deliberately: reading your memberships is answering a
-- question about yourself; granting yourself a role is not. Writes keep going
-- through tenant_isolation alone.
--
-- app.current_user is also what the audit log will need in order to name the
-- author of a change (RNF-12), which today the database has no way to know.
-- =============================================================================

CREATE OR REPLACE FUNCTION current_user_id() RETURNS uuid AS $fn$
    SELECT NULLIF(current_setting('app.current_user', true), '')::uuid;
$fn$ LANGUAGE sql STABLE;

COMMENT ON FUNCTION current_user_id() IS
  'The person the current transaction acts as, or NULL when none was
   established. Set with set_config(..., true) so it lasts for the
   transaction and never for the pooled connection, exactly like
   app.current_org.';

-- Permissive, so it is OR-ed with tenant_isolation: a membership row is
-- visible when it belongs to the established organization, or when it names
-- the established person.
CREATE POLICY own_memberships ON organization_memberships
    FOR SELECT
    USING (user_id = current_user_id());

COMMENT ON POLICY own_memberships ON organization_memberships IS
  'Lets a person read the memberships that name them, with no organization
   established. Without it, signing in cannot discover which organizations
   the account belongs to, since that lookup precedes any context.';

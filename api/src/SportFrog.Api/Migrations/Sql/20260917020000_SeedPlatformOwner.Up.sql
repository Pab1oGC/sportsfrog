-- =============================================================================
-- SeedPlatformOwner
--
-- The one organization and account allowed to register new organizations
-- now that POST /organizations is no longer anonymous (see
-- PlatformAuthority and RegisterOrganization) -- a fresh database needs at
-- least one such account before anyone can create the first customer
-- organization at all. Frogtech Solutions is SportFrog's own operating
-- account, the same organization RestrictPublicPortalToFrogtech already
-- treats as the one actually being demonstrated -- not a customer's, and
-- not something an ordinary caller can create or rename.
--
-- Idempotent on the organization's slug and the account's email: safe to
-- re-run, and safe to run against a database where either already exists
-- from earlier manual testing -- it reuses whichever row is already there
-- instead of erroring or duplicating. Granting 'owner' again for an account
-- that already holds it is a no-op via the last ON CONFLICT.
--
-- {{EMAIL}} and {{PASSWORD_HASH}} are substituted by SeedPlatformOwner.cs
-- from the PLATFORM_ADMIN_EMAIL / PLATFORM_ADMIN_PASSWORD environment
-- variables before this ever reaches the database -- this file never holds
-- a real secret, seeded or not.
--
-- A PL/pgSQL block, not three bare statements: organization_memberships
-- carries the same isolation policy every other business table does (see
-- current_org_id() in InitialSchema), enforced with FORCE ROW LEVEL
-- SECURITY so even the schema owner applying this migration is subject to
-- it. The membership insert needs app.current_org set to this organization's
-- id first -- the same set_config call RegisterOrganization.PersistAsync
-- makes at runtime, mid-transaction, once there is an organization to
-- establish it to -- and that id is only known after the insert above,
-- whether it ran or was skipped by ON CONFLICT.
-- =============================================================================

DO $$
DECLARE
    v_org_id uuid;
BEGIN
    INSERT INTO organizations (name, slug)
    VALUES ('Frogtech Solutions', 'frogtech-solutions')
    ON CONFLICT (slug) DO NOTHING;

    SELECT id INTO v_org_id FROM organizations WHERE slug = 'frogtech-solutions';

    INSERT INTO users (email, password_hash, full_name)
    VALUES ('{{EMAIL}}', '{{PASSWORD_HASH}}', 'Frogtech Solutions')
    ON CONFLICT (email) DO NOTHING;

    PERFORM set_config('app.current_org', v_org_id::text, true);

    INSERT INTO organization_memberships (org_id, user_id, role)
    SELECT v_org_id, u.id, 'owner'
    FROM users u
    WHERE u.email = '{{EMAIL}}'
    ON CONFLICT (org_id, user_id) DO NOTHING;
END $$;

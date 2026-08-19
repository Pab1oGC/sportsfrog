-- =============================================================================
-- FixPublicCompetitionResolution
--
-- resolve_public_competition could never return a row.
--
-- The function is SECURITY DEFINER so it runs as the schema owner, on the
-- assumption that this lets it read competitions before any isolation context
-- exists. It does not: competitions carries FORCE ROW LEVEL SECURITY, and
-- FORCE applies the policy to the table owner as well. Running as the owner
-- with no context established, the policy matched nothing, so every public
-- address resolved to "not found".
--
-- Keeping FORCE is the right call — it is what stops a careless query made by
-- the owner from crossing organizations — so the function is the part that
-- changes: it now establishes the context itself, in the only order the
-- design allows.
--
--   1. organizations carries no policy, so the slug can be resolved with no
--      context at all.
--   2. That organization becomes the context for the current transaction.
--   3. Only then can competitions be read, and the policy is satisfied.
--
-- If step 3 finds nothing, the context is cleared again before returning. An
-- address that names a real organization but no publishable competition must
-- not leave the caller able to read that organization's rows: knowing a slug
-- is not authorization to read a roster (RNF-16).
--
-- VOLATILE, not STABLE: the function changes transaction state, and a planner
-- free to skip repeated calls could leave the context unset.
-- =============================================================================

CREATE OR REPLACE FUNCTION resolve_public_competition(p_org_slug citext, p_comp_slug citext)
RETURNS TABLE (org_id uuid, competition_id uuid)
LANGUAGE plpgsql SECURITY DEFINER VOLATILE AS $fn$
DECLARE
    v_org_id         uuid;
    v_competition_id uuid;
BEGIN
    SELECT o.id INTO v_org_id
    FROM organizations o
    WHERE o.slug = p_org_slug
      AND o.is_active
      AND o.deleted_at IS NULL;

    IF v_org_id IS NULL THEN
        RETURN;
    END IF;

    -- set_config with is_local = true is SET LOCAL: it lasts for this
    -- transaction and is gone before the connection returns to the pool.
    PERFORM set_config('app.current_org', v_org_id::text, true);

    SELECT c.id INTO v_competition_id
    FROM competitions c
    WHERE c.org_id = v_org_id
      AND c.slug = p_comp_slug
      AND c.is_public
      AND c.deleted_at IS NULL;

    IF v_competition_id IS NULL THEN
        PERFORM set_config('app.current_org', '', true);
        RETURN;
    END IF;

    RETURN QUERY SELECT v_org_id, v_competition_id;
END $fn$;

-- CREATE OR REPLACE keeps the existing privileges, but they are restated so
-- that applying this migration to a database built another way ends up in the
-- same place.
REVOKE ALL ON FUNCTION resolve_public_competition(citext, citext) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION resolve_public_competition(citext, citext) TO sportfrog_public;

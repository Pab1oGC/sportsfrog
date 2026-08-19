-- =============================================================================
-- FixPublicCompetitionResolution - rollback
--
-- Restores the original definition from InitialSchema, so that reverting this
-- migration leaves the schema exactly as the previous one produced it.
--
-- Note that the restored function does not work: it reads competitions with
-- no isolation context established, and FORCE ROW LEVEL SECURITY makes the
-- policy apply to the owner it runs as, so it matches no rows. That is the
-- state being returned to, not a state worth staying in — the only reason to
-- revert this migration is to revert the one after it too.
-- =============================================================================

CREATE OR REPLACE FUNCTION resolve_public_competition(p_org_slug citext, p_comp_slug citext)
RETURNS TABLE (org_id uuid, competition_id uuid)
LANGUAGE sql SECURITY DEFINER STABLE AS $fn$
    SELECT o.id, c.id
    FROM organizations o
    JOIN competitions c ON c.org_id = o.id
    WHERE o.slug = p_org_slug
      AND c.slug = p_comp_slug
      AND o.is_active
      AND o.deleted_at IS NULL
      AND c.is_public
      AND c.deleted_at IS NULL;
$fn$;

REVOKE ALL ON FUNCTION resolve_public_competition(citext, citext) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION resolve_public_competition(citext, citext) TO sportfrog_public;

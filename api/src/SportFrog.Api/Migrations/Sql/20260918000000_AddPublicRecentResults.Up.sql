-- =============================================================================
-- AddPublicRecentResults
--
-- The landing hero wants real results floating around the wordmark instead
-- of invented ones (PRODUCT.md: "Honest numbers only. No fabricated
-- metrics, ever."). That needs one function that crosses organizations, the
-- same reason list_public_competitions exists: a feed of "what just
-- happened, anywhere" has no single organization to scope a context to.
--
-- Same shape as list_public_competitions and resolve_public_competition
-- (AddPublicCompetitionDirectory, RestrictPublicPortalToFrogtech): SECURITY
-- DEFINER is not needed here since this reads through plain INNER JOINs
-- under sportfrog_public's own row-level policies, but the organization
-- restriction is the same TEMPORARY line for the same reason -- this
-- database still carries dozens of leftover test organizations with no way
-- to unpublish them one by one yet.
--
-- home_team_id/away_team_id can be null since AddBracketPlaceholderSlots (a
-- knockout drawn in full books a later round before it knows who reaches
-- it). No explicit null check is needed: an INNER JOIN against teams simply
-- drops a row whose side is not a real team id yet, which is exactly the
-- right answer -- a ticker of "what just happened" has nothing to say about
-- a fixture nobody has played into existence.
--
-- matches carries FORCE ROW LEVEL SECURITY and, until now, only
-- tenant_isolation -- every public reading that touches it so far went
-- through PublicCompetitionReader, which establishes a real organization
-- context first. This is the first public reading with no single
-- organization to establish one for, so it needs the same permissive branch
-- AddPublicCompetitionDirectory already added to competitions/categories/
-- teams, or sportfrog_public reads zero rows here in silence -- FORCE means
-- even a function running with elevated privileges would see nothing
-- without it, so a policy is the only way in, not a workaround.
-- =============================================================================

CREATE POLICY published_matches ON matches
    FOR SELECT TO sportfrog_public
    USING (deleted_at IS NULL AND EXISTS (
        SELECT 1 FROM competitions c
        WHERE c.id = matches.competition_id
          AND c.is_public
          AND c.deleted_at IS NULL));

COMMENT ON POLICY published_matches ON matches IS
  'Lets a published competition''s matches be read with no organization
   established -- same reason and same shape as published_teams. Narrow on
   the same three axes: only matches of a competition its organization
   published, only for SELECT, only for the read-only public role.';

CREATE OR REPLACE FUNCTION list_public_recent_results(p_take int DEFAULT 8)
RETURNS TABLE (
    organization_slug citext,
    competition_slug  citext,
    competition_name  text,
    category_name     text,
    sport_code        text,
    sport_name        text,
    home_team_name    text,
    away_team_name    text,
    home_total        integer,
    away_total        integer,
    status            match_state,
    scheduled_at      timestamptz)
LANGUAGE sql STABLE AS $fn$
    SELECT
        o.slug,
        c.slug,
        c.name,
        k.name,
        s.code,
        s.name,
        ht.name,
        at.name,
        m.home_total,
        m.away_total,
        m.status,
        m.scheduled_at
    FROM matches m
    JOIN competitions c   ON c.id = m.competition_id
    JOIN organizations o  ON o.id = c.org_id
    JOIN sports s         ON s.code = c.sport_code
    JOIN categories k     ON k.id = m.category_id
    JOIN teams ht         ON ht.id = m.home_team_id
    JOIN teams at         ON at.id = m.away_team_id
    WHERE m.deleted_at IS NULL
      AND c.is_public
      AND c.deleted_at IS NULL
      AND o.is_active
      AND o.deleted_at IS NULL
      AND o.slug = 'frogtech-solutions' -- TEMPORARY, see RestrictPublicPortalToFrogtech.
      AND m.status IN ('in_progress', 'finished', 'walkover')
    ORDER BY
        CASE m.status WHEN 'in_progress' THEN 0 ELSE 1 END,
        m.scheduled_at DESC NULLS LAST
    LIMIT greatest(1, least(coalesce(p_take, 8), 24));
$fn$;

COMMENT ON FUNCTION list_public_recent_results(int) IS
  'What just happened, or is happening, across every published competition --
   the feed the landing hero reads to float real results instead of
   decoration. In-progress first, then most recently scheduled finished/
   walkover results. Runs as its caller (sportfrog_public), so published_matches
   and its siblings are what let it see anything at all.';

REVOKE ALL ON FUNCTION list_public_recent_results(int) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION list_public_recent_results(int) TO sportfrog_public;

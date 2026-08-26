-- =============================================================================
-- AddPublicCompetitionDirectory
--
-- Until now the public view could only be reached by somebody who already knew
-- the address: /public/{organización}/{competencia}. That is fine for a link
-- somebody was sent and useless as a front page, where a visitor arrives
-- knowing nothing and expects to be shown what there is.
--
-- This is the one public reading that deliberately crosses organizations, and
-- that is why it cannot use the existing resolver. Every other public path
-- establishes an isolation context for one organization and reads inside it; a
-- directory has no single organization to establish, so it can never go
-- through the policies the way the others do.
--
-- Two things had to be true to solve that without weakening anything.
--
-- First: an owner-privileged function is not the answer, and it is worth
-- saying why because it looks like it should be. These tables are FORCE ROW
-- LEVEL SECURITY, and FORCE means the policies apply to the owner as well. A
-- SECURITY DEFINER function with no established organization reads exactly
-- nothing — silently, as an empty list rather than an error.
--
-- Second: the policies gain a permissive branch, and it is granted TO
-- sportfrog_public and to nobody else. That is the whole safety of this
-- design. The public role already cannot write anything; giving only it the
-- ability to see published rows without a context means the authenticated API
-- keeps exactly the isolation it had. Without the role restriction, a signed-in
-- operator of one league would start seeing another league's categories
-- through the ordinary endpoints — which is not a disclosure of anything
-- secret, but is a rule quietly changing underneath code that was written
-- assuming it.
--
-- The argument for the branch itself is the same one own_memberships rests on:
-- isolation exists to keep one organization's private data from another, and
-- publishing is the act of declaring that this data is not private. Every
-- field this directory reads is already on the public page of that
-- competition. What changes is that a visitor no longer has to know the
-- address to find it.
-- =============================================================================

-- Permissive, so they are OR-ed with tenant_isolation. FOR SELECT only: a
-- competition being public says something about who may read it and nothing
-- about who may change it. And TO sportfrog_public, so the read-only role is
-- the only one that gains anything at all.

CREATE POLICY published_competitions ON competitions
    FOR SELECT TO sportfrog_public
    USING (is_public AND deleted_at IS NULL);

CREATE POLICY published_categories ON categories
    FOR SELECT TO sportfrog_public
    USING (EXISTS (
        SELECT 1 FROM competitions c
        WHERE c.id = categories.competition_id
          AND c.is_public
          AND c.deleted_at IS NULL));

CREATE POLICY published_teams ON teams
    FOR SELECT TO sportfrog_public
    USING (deleted_at IS NULL AND EXISTS (
        SELECT 1 FROM categories k
        JOIN competitions c ON c.id = k.competition_id
        WHERE k.id = teams.category_id
          AND c.is_public
          AND c.deleted_at IS NULL));

COMMENT ON POLICY published_competitions ON competitions IS
  'Lets a published competition be read with no organization established, which
   is what the public directory needs and what no other reading has ever
   required. Narrow on three axes: only rows the organization published, only
   for SELECT, and only for the read-only role that serves the public portal.';

-- Nothing in the schema made "the published ones" a cheap question: the only
-- index on competitions leads with org_id, which is exactly the column this
-- query does not have.
CREATE INDEX idx_competitions_public
    ON competitions(status, starts_on DESC)
    WHERE is_public AND deleted_at IS NULL;

-- Runs as whoever calls it — sportfrog_public — precisely so the policy above
-- applies. Made SECURITY DEFINER it would run as the owner, which FORCE row
-- level security leaves seeing nothing.
CREATE OR REPLACE FUNCTION list_public_competitions(
    p_sport  text DEFAULT NULL,
    p_season text DEFAULT NULL,
    p_search text DEFAULT NULL,
    p_take   int  DEFAULT 24,
    p_skip   int  DEFAULT 0)
RETURNS TABLE (
    organization_slug citext,
    organization_name text,
    competition_slug  citext,
    competition_name  text,
    sport_code        text,
    sport_name        text,
    season            text,
    status            competition_state,
    starts_on         date,
    ends_on           date,
    categories        bigint,
    teams             bigint,
    total             bigint)
LANGUAGE sql STABLE AS $fn$
    SELECT
        o.slug,
        o.name,
        c.slug,
        c.name,
        s.code,
        s.name,
        c.season,
        c.status,
        c.starts_on,
        c.ends_on,
        (SELECT count(*) FROM categories k WHERE k.competition_id = c.id),
        (SELECT count(*) FROM teams t
           JOIN categories k ON k.id = t.category_id
          WHERE k.competition_id = c.id AND t.deleted_at IS NULL),

        -- The size of the whole answer, carried on every row. A portal needs
        -- it to draw pagination, and asking for it separately would mean two
        -- queries whose filters have to be kept identical by hand.
        count(*) OVER ()
    FROM competitions c
    JOIN organizations o ON o.id = c.org_id
    JOIN sports s ON s.code = c.sport_code
    WHERE c.is_public
      AND c.deleted_at IS NULL

      -- The same two conditions on the organization that
      -- resolve_public_competition applies. Stated again rather than shared,
      -- because they are the definition of "reachable" and the two must agree:
      -- if they ever drift, this list offers pages that do not open.
      AND o.is_active
      AND o.deleted_at IS NULL

      AND (p_sport  IS NULL OR c.sport_code = p_sport)
      AND (p_season IS NULL OR c.season = p_season)
      AND (p_search IS NULL OR c.name ILIKE '%' || p_search || '%'
                            OR o.name ILIKE '%' || p_search || '%')

    -- What a visitor came to see, in the order they came to see it: what is
    -- being played now, then what is about to be, then what has finished.
    ORDER BY
        CASE c.status
            WHEN 'in_progress' THEN 0
            WHEN 'scheduled'   THEN 1
            WHEN 'finished'    THEN 2
            ELSE 3
        END,
        c.starts_on DESC NULLS LAST,
        c.name
    LIMIT  greatest(1, least(coalesce(p_take, 24), 100))
    OFFSET greatest(0, coalesce(p_skip, 0));
$fn$;

COMMENT ON FUNCTION list_public_competitions(text, text, text, int, int) IS
  'The public directory of competitions, across every organization.

   Runs as its caller, which is the read-only role, so the published_* policies
   are what let it see anything at all. Deliberately narrow: it returns what a
   listing shows and nothing else — no identifiers, no settings, no counts of
   people — and only for competitions their organization chose to publish.

   Filtering and ordering happen here rather than in the application so that
   the LIMIT is applied by the database and a portal on page one does not read
   every published competition on the platform.';

REVOKE ALL ON FUNCTION list_public_competitions(text, text, text, int, int) FROM PUBLIC;

-- Only the read-only role. The application role would gain nothing by calling
-- it — it has no permissive branch and no context — and granting it anyway
-- would suggest otherwise.
GRANT EXECUTE ON FUNCTION list_public_competitions(text, text, text, int, int) TO sportfrog_public;

-- No new table grants. organizations, competitions, categories, teams and
-- sports are all already readable by the public role; what changes is that
-- there is now a way to ask about them without naming an organization first.

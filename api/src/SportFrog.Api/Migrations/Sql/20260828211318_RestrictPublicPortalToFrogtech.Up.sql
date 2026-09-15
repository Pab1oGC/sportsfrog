-- =============================================================================
-- RestrictPublicPortalToFrogtech
--
-- TEMPORARY. This database carries dozens of organizations left over from
-- earlier rounds of testing this project, none of them cleaned up, and none
-- of them reachable through the application to unpublish one by one — there
-- is no owner login for most of them, and no DeleteOrganization endpoint
-- exists to remove them outright. Until that gets a real answer, the public
-- portal is narrowed to the one organization that is actually being
-- demonstrated, so a visitor sees that and nothing else.
--
-- Done at the two functions that decide what the public portal can see at
-- all, rather than by touching is_public on every other organization's
-- competitions: nothing here is data, so nothing here needs to be undone row
-- by row later — dropping this migration (or replacing both functions again
-- without the added line) is the whole rollback, the moment there is a real
-- way to publish and unpublish per organization instead.
--
-- resolve_public_competition is where a deep link to any other organization's
-- page gets refused, the same way the function already refuses a slug that
-- does not exist — RNF-16 already required that a caller learn nothing about
-- an organization it does not have the slug's competition published, so this
-- is the same refusal on one more condition. list_public_competitions is
-- where that organization stops appearing on the front page's directory.
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

REVOKE ALL ON FUNCTION resolve_public_competition(citext, citext) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION resolve_public_competition(citext, citext) TO sportfrog_public;

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
        count(*) OVER ()
    FROM competitions c
    JOIN organizations o ON o.id = c.org_id
    JOIN sports s ON s.code = c.sport_code
    WHERE c.is_public
      AND c.deleted_at IS NULL
      AND o.is_active
      AND o.deleted_at IS NULL

      AND (p_sport  IS NULL OR c.sport_code = p_sport)
      AND (p_season IS NULL OR c.season = p_season)
      AND (p_search IS NULL OR c.name ILIKE '%' || p_search || '%'
                            OR o.name ILIKE '%' || p_search || '%')
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

REVOKE ALL ON FUNCTION list_public_competitions(text, text, text, int, int) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION list_public_competitions(text, text, text, int, int) TO sportfrog_public;

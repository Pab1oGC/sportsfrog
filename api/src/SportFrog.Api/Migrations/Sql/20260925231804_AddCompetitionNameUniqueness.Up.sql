-- =============================================================================
-- AddCompetitionNameUniqueness
--
-- Two competitions of the same organization could share a name: only the slug
-- was unique, and CreateCompetition generates a fresh slug on request, so
-- nothing stopped "La Liga 25/26" from existing twice. Organizers name their
-- competitions with the season built in, so a repeated name is a mistake and
-- not a legitimate second edition.
--
-- Unique among the living and case-insensitive, like uq_competitions_slug: a
-- name freed by a logical deletion can be taken again, and "La Liga" and
-- "la liga" are the same name to whoever reads the public page.
--
-- The index would refuse to build over rows that already collide, so the
-- ones that do are renamed first: the oldest keeps its name and each later
-- one gets " (2)", " (3)"... appended. The slug is not touched -- it is the
-- public address and is locked from creation (see UpdateCompetition).
-- =============================================================================

-- competitions is under FORCE ROW LEVEL SECURITY, so the owner running this
-- migration sees no row unless an organization context is set. It is set per
-- organization, local to this transaction, and cleared at the end.
DO $do$
DECLARE
    org uuid;
BEGIN
    FOR org IN SELECT id FROM organizations LOOP
        PERFORM set_config('app.current_org', org::text, true);

        WITH ranked AS (
            SELECT id,
                   name,
                   row_number() OVER (
                       PARTITION BY lower(name) ORDER BY created_at, id) AS position
            FROM competitions
            WHERE deleted_at IS NULL
        )
        UPDATE competitions c
        SET name = ranked.name || ' (' || ranked.position || ')'
        FROM ranked
        WHERE ranked.id = c.id
          AND ranked.position > 1;
    END LOOP;

    PERFORM set_config('app.current_org', '', true);
END $do$;

CREATE UNIQUE INDEX uq_competitions_name
    ON competitions (org_id, lower(name))
    WHERE deleted_at IS NULL;

COMMENT ON INDEX uq_competitions_name IS
  'One living competition per name within an organization, ignoring case.
   Answers the race that CreateCompetition and UpdateCompetition''s own
   pre-check cannot see.';

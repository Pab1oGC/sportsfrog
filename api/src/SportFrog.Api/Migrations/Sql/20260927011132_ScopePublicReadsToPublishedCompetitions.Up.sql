-- =============================================================================
-- ScopePublicReadsToPublishedCompetitions
--
-- 'tenant_isolation' (USING org_id = current_org_id()) is defined on every
-- business table with an `org_id`, and it applies to role {public} -- meaning
-- literally any role, sportfrog_public included. current_org_id() is just
-- current_setting('app.current_org'), and set_config() has no permission
-- check of its own: any role that can open a connection can call it.
--
-- The application never does that for the public role except after a slug
-- has already been validated by resolve_public_competition() /
-- resolve_public_document(). But a direct SQL session authenticated as
-- sportfrog_public can set app.current_org to *any* organization's id --
-- discoverable at no cost, since 'organizations' carries no RLS at all --
-- and then read every row tenant_isolation alone would otherwise gate for
-- that organization: full athlete records (document id, birth date,
-- guardian phone), rosters, unpublished competitions, all of it. That is
-- the actual, confirmed database-level guarantee today, independent of
-- anything the API's own code chooses to expose.
--
-- The fix has two parts:
--
--   1. Narrow tenant_isolation's role to sportfrog_app only, on every table
--      that has it. sportfrog_app already reaches everything it needs
--      through the application's own tenancy middleware; nothing about its
--      access changes.
--
--   2. For the handful of tables the public portal legitimately reads
--      (roster names, top-scorer names, venue names, a competition's own
--      ruleset), add a policy scoped the same way 'published_competitions'
--      already scopes competitions/categories/teams/matches: visible only
--      when it traces back to a competition that is public and not
--      deleted. That is the same shape this schema already trusted for
--      those four tables -- it just never reached the tables underneath
--      them.
--
-- Four tables already carry BOTH tenant_isolation and a published_* policy
-- (categories, competitions, matches, teams): before this migration,
-- sportfrog_public could still see an org's *unpublished* rows there too,
-- through tenant_isolation alone, once app.current_org was set to that org.
-- Narrowing tenant_isolation closes that gap for them as well, without
-- touching their existing published_* policies at all.
--
-- Eight tables get a brand new published_* policy: athletes, clubs,
-- performances, player_events, roster_entries, rulesets, venue_spaces,
-- venues. Every one of them is read by a real public endpoint today
-- (public roster, public leaderboards, public calendar, public
-- classification for individual sports) -- traced by hand against the
-- actual query code, not guessed. Skipping any of them would silently
-- empty out part of the public portal instead of closing a hole.
--
-- issued_documents keeps its own tenant_isolation-only policy and gets no
-- published_* replacement: the one endpoint that read it as
-- sportfrog_public (VerifyDocument, public credential verification) is not
-- mapped today (see Program.cs). Its SELECT grant to sportfrog_public is
-- left in place rather than revoked, so re-enabling that endpoint later is
-- a routing change, not a grants change -- it will simply see nothing
-- until a published_issued_documents-shaped policy is added for it, which
-- is the correct failure mode (empty, not an error) if anyone re-enables
-- the route without reading this comment.
--
-- sports and sport_metrics are untouched: global catalog tables, no
-- org_id, never had RLS, and that was already correct.
-- =============================================================================

-- --- 1. Narrow tenant_isolation everywhere it exists --------------------

DO $$
DECLARE
    isolated_table text;
BEGIN
    FOREACH isolated_table IN ARRAY ARRAY[
        'athletes', 'audit_log', 'categories', 'clubs', 'competitions',
        'document_batches', 'document_template_versions', 'document_templates',
        'issued_documents', 'matches', 'organization_memberships', 'performances',
        'photo_imports', 'player_events', 'roster_entries', 'rulesets', 'teams',
        'venue_spaces', 'venues'
    ]
    LOOP
        EXECUTE format('ALTER POLICY tenant_isolation ON %I TO sportfrog_app', isolated_table);
    END LOOP;
END $$;

-- --- 2. Publish exactly what the public portal reads today --------------

-- Athletes: a name shows up on a public roster or leaderboard only when the
-- athlete is (non-withdrawn, non-deleted) on a team belonging to a
-- published, non-deleted competition. This narrows "any athlete in the
-- organization" down to "an athlete whose organization already chose to
-- publish this particular roster" -- real columns beyond first/last name
-- (document id, birth date, guardian phone) are still readable by a direct
-- row query at that narrower scope; the application's own SELECT
-- projections are what keep those columns from ever being served. Closing
-- that further needs a column-level mechanism (a view or a narrower
-- function), which is a follow-up, not part of this migration.
CREATE POLICY published_athletes ON athletes
    FOR SELECT TO sportfrog_public
    USING (
        deleted_at IS NULL
        AND EXISTS (
            SELECT 1
            FROM roster_entries re
            JOIN teams t ON t.id = re.team_id
            JOIN categories k ON k.id = t.category_id
            JOIN competitions c ON c.id = k.competition_id
            WHERE re.athlete_id = athletes.id
              AND re.deleted_at IS NULL
              AND t.deleted_at IS NULL
              AND c.is_public
              AND c.deleted_at IS NULL
        )
    );

-- Clubs: read for their name/logo on a public roster.
CREATE POLICY published_clubs ON clubs
    FOR SELECT TO sportfrog_public
    USING (
        deleted_at IS NULL
        AND EXISTS (
            SELECT 1
            FROM teams t
            JOIN categories k ON k.id = t.category_id
            JOIN competitions c ON c.id = k.competition_id
            WHERE t.club_id = clubs.id
              AND t.deleted_at IS NULL
              AND c.is_public
              AND c.deleted_at IS NULL
        )
    );

-- Performances: individual-sport classification (taekwondo and the like),
-- read by the public "classification" endpoint. Unlike team sports,
-- competition_id sits directly on the row.
CREATE POLICY published_performances ON performances
    FOR SELECT TO sportfrog_public
    USING (
        deleted_at IS NULL
        AND EXISTS (
            SELECT 1
            FROM competitions c
            WHERE c.id = performances.competition_id
              AND c.is_public
              AND c.deleted_at IS NULL
        )
    );

-- Player events: every goal/card a public leaderboard or live match view
-- reads. No deleted_at of its own -- events are corrected or removed
-- outright, never soft-deleted.
CREATE POLICY published_player_events ON player_events
    FOR SELECT TO sportfrog_public
    USING (
        EXISTS (
            SELECT 1
            FROM matches m
            JOIN categories k ON k.id = m.category_id
            JOIN competitions c ON c.id = k.competition_id
            WHERE m.id = player_events.match_id
              AND m.deleted_at IS NULL
              AND c.is_public
              AND c.deleted_at IS NULL
        )
    );

-- Roster entries: the rows the public roster and calendar read directly.
CREATE POLICY published_roster_entries ON roster_entries
    FOR SELECT TO sportfrog_public
    USING (
        deleted_at IS NULL
        AND EXISTS (
            SELECT 1
            FROM teams t
            JOIN categories k ON k.id = t.category_id
            JOIN competitions c ON c.id = k.competition_id
            WHERE t.id = roster_entries.team_id
              AND t.deleted_at IS NULL
              AND c.is_public
              AND c.deleted_at IS NULL
        )
    );

-- Rulesets: standings and leaderboards read the ruleset a public
-- competition is scored under (points, tiebreakers, metric labels). No
-- deleted_at of its own -- a ruleset in use is never deleted.
CREATE POLICY published_rulesets ON rulesets
    FOR SELECT TO sportfrog_public
    USING (
        EXISTS (
            SELECT 1
            FROM competitions c
            WHERE c.ruleset_id = rulesets.id
              AND c.is_public
              AND c.deleted_at IS NULL
        )
    );

-- Venue spaces: read for a match's or a performance's location on the
-- public calendar. No deleted_at of its own.
CREATE POLICY published_venue_spaces ON venue_spaces
    FOR SELECT TO sportfrog_public
    USING (
        EXISTS (
            SELECT 1
            FROM matches m
            JOIN categories k ON k.id = m.category_id
            JOIN competitions c ON c.id = k.competition_id
            WHERE m.venue_space_id = venue_spaces.id
              AND m.deleted_at IS NULL
              AND c.is_public
              AND c.deleted_at IS NULL
        )
        OR EXISTS (
            SELECT 1
            FROM performances p
            JOIN competitions c ON c.id = p.competition_id
            WHERE p.venue_space_id = venue_spaces.id
              AND p.deleted_at IS NULL
              AND c.is_public
              AND c.deleted_at IS NULL
        )
    );

-- Venues: the same reasoning, one hop further out through venue_spaces.
CREATE POLICY published_venues ON venues
    FOR SELECT TO sportfrog_public
    USING (
        EXISTS (
            SELECT 1
            FROM venue_spaces vs
            JOIN matches m ON m.venue_space_id = vs.id
            JOIN categories k ON k.id = m.category_id
            JOIN competitions c ON c.id = k.competition_id
            WHERE vs.venue_id = venues.id
              AND m.deleted_at IS NULL
              AND c.is_public
              AND c.deleted_at IS NULL
        )
        OR EXISTS (
            SELECT 1
            FROM venue_spaces vs
            JOIN performances p ON p.venue_space_id = vs.id
            JOIN competitions c ON c.id = p.competition_id
            WHERE vs.venue_id = venues.id
              AND p.deleted_at IS NULL
              AND c.is_public
              AND c.deleted_at IS NULL
        )
    );

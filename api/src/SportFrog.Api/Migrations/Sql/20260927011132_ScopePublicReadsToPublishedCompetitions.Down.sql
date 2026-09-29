-- =============================================================================
-- ScopePublicReadsToPublishedCompetitions - rollback
--
-- Undoes the .Up.sql file in reverse order: drop the eight new policies
-- first, then widen tenant_isolation back to {public} everywhere. Reverses
-- the fix exactly, including the gap it closed -- this is a rollback, not a
-- judgment call about which state is correct.
-- =============================================================================

DROP POLICY published_venues ON venues;
DROP POLICY published_venue_spaces ON venue_spaces;
DROP POLICY published_rulesets ON rulesets;
DROP POLICY published_roster_entries ON roster_entries;
DROP POLICY published_player_events ON player_events;
DROP POLICY published_performances ON performances;
DROP POLICY published_clubs ON clubs;
DROP POLICY published_athletes ON athletes;

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
        EXECUTE format('ALTER POLICY tenant_isolation ON %I TO public', isolated_table);
    END LOOP;
END $$;

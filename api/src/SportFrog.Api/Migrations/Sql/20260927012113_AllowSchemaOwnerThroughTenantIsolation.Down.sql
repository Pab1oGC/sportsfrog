-- =============================================================================
-- AllowSchemaOwnerThroughTenantIsolation - rollback
--
-- Narrows tenant_isolation back to sportfrog_app only, undoing this
-- migration's fix. Rolling back past this point without also rolling back
-- ScopePublicReadsToPublishedCompetitions reintroduces the regression this
-- migration fixed (resolve_public_competition / resolve_public_document
-- returning nothing) -- roll both back together, in order.
-- =============================================================================

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

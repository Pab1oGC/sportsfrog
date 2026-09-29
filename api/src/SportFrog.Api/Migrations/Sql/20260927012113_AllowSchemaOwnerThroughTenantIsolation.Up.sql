-- =============================================================================
-- AllowSchemaOwnerThroughTenantIsolation
--
-- Fixes a regression the previous migration introduced within this same
-- session, caught by re-running the public portal end to end rather than
-- assumed fixed: narrowing tenant_isolation's role to sportfrog_app alone
-- silently broke resolve_public_competition() and resolve_public_document().
--
-- Both are SECURITY DEFINER, so they execute as sportfrog_owner -- and
-- FORCE ROW LEVEL SECURITY (already the reason FixPublicCompetitionResolution
-- exists) means even the schema owner needs a policy that actually applies to
-- it. Before ScopePublicReadsToPublishedCompetitions, tenant_isolation's role
-- was {public}, which covered sportfrog_owner incidentally, alongside
-- sportfrog_app and sportfrog_public. Narrowing it to sportfrog_app only left
-- sportfrog_owner covered by nothing on 'competitions' and 'issued_documents'
-- the moment those two functions set app.current_org and queried past it --
-- both now returned zero rows for a competition or document that genuinely
-- exists and is genuinely public.
--
-- This adds sportfrog_owner back to tenant_isolation's role, everywhere it
-- was narrowed. It does not touch sportfrog_public's access at all -- the
-- role this whole change was about keeping out never appears here again.
-- sportfrog_owner isn't a role any public-facing connection string uses (see
-- ConnectionStrings__Migrations, api/README.md): the two SECURITY DEFINER
-- functions are the only callers this restores, and both still validate slug,
-- publication and activity before ever reaching a row tenant_isolation would
-- gate.
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
        EXECUTE format('ALTER POLICY tenant_isolation ON %I TO sportfrog_app, sportfrog_owner', isolated_table);
    END LOOP;
END $$;

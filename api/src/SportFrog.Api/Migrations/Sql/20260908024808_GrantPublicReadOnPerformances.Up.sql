-- =============================================================================
-- GrantPublicReadOnPerformances
--
-- AddPerformances granted the table to sportfrog_app only, because at the
-- time nothing public read it. The public portal's classification page
-- needs the same table, through the same tenant_isolation policy every
-- other public reading already relies on (teams, matches, roster_entries):
-- PublicCompetitionReader sets app.current_org to the competition's own
-- organization before this runs, so the existing policy already scopes the
-- rows correctly. What is missing is the grant itself — RLS decides which
-- rows a role may see, not whether it may query the table at all, and
-- sportfrog_public was never given SELECT here.
--
-- No new policy, no new function: this is a widening of who may read,
-- nothing about what is readable.
-- =============================================================================

GRANT SELECT ON performances TO sportfrog_public;

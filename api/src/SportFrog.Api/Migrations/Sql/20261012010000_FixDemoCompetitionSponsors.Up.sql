-- =============================================================================
-- FixDemoCompetitionSponsors
--
-- 20261012000000_SeedDemoCompetitionsFrogtech copied the taekwondo portal
-- settings with the sponsors' logo_key removed, leaving sponsor entries with
-- a name and no logo. SponsorLink.LogoKey is a required string, so reading
-- that competition's settings throws, and because GET /competitions
-- deserializes every competition of the organization, one such row made the
-- whole listing fail.
--
-- Empties the sponsors of those three demo competitions when any entry lacks
-- a logo_key. Scoped to their ids: it touches nothing else. organization
-- tables are under FORCE ROW LEVEL SECURITY, so app.current_org is set to
-- each organization before the update.
-- =============================================================================

DO $$
DECLARE
    org RECORD;
BEGIN
    FOR org IN SELECT id FROM organizations ORDER BY id LOOP
        PERFORM set_config('app.current_org', org.id::text, true);

        UPDATE competitions
        SET settings = jsonb_set(settings, '{public,sponsors}', '[]'::jsonb)
        WHERE id IN (
                '97997415-7e0e-4705-89e0-ce9d597f3856',
                '45708442-acb4-408d-8f2e-3a4e0d305a20',
                'ece3af57-b4f6-465d-9460-41f8a567de6c')
          AND jsonb_typeof(settings #> '{public,sponsors}') = 'array'
          AND EXISTS (
                SELECT 1
                FROM jsonb_array_elements(settings #> '{public,sponsors}') AS sponsor
                WHERE NOT (sponsor ? 'logo_key'));
    END LOOP;

    PERFORM set_config('app.current_org', '', true);
END
$$;

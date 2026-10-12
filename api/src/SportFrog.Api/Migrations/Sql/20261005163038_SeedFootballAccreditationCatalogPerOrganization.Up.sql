-- =============================================================================
-- SeedFootballAccreditationCatalogPerOrganization
--
-- Re-applies the starting football accreditation catalogue to every active
-- football competition that still has none, and does it correctly this time.
--
-- Why a new migration instead of fixing 20261003012208: that one already ran
-- and is history. Its loop read competitions as sportfrog_owner, which is
-- subject to tenant_isolation (the role has no BYPASSRLS), and no organization
-- was set in that session, so current_org_id() was NULL and the loop saw zero
-- competitions. It recorded itself as applied and inserted nothing.
--
-- This version walks the organizations (which carry no RLS) and sets
-- app.current_org to each one before reading its competitions, so the same
-- guarded loop finds them and the isolation policy accepts the inserts.
--
-- The content is identical to 20261003012208. Guarded per competition, so it
-- is harmless where the catalogue already exists, including the one
-- competition in a local database that has a hand-made item.
-- =============================================================================

DO $$
DECLARE
    org RECORD;
    comp RECORD;
    item_fut  uuid;
    item_vil  uuid;
    item_mpc  uuid;
    item_est  uuid;
    item_ta   uuid;
    item_com  uuid;
    item_blue uuid;
    item_prep uuid;
    item_res  uuid;
    cat_aa    uuid;
    cat_ao    uuid;
BEGIN
    FOR org IN SELECT id FROM organizations ORDER BY id LOOP
        -- Transaction-local: it ends with the migration, and the reset after
        -- the loop makes the empty value explicit.
        PERFORM set_config('app.current_org', org.id::text, true);

        FOR comp IN
            SELECT id, org_id
            FROM competitions
            WHERE sport_code = 'football'
              AND deleted_at IS NULL
              AND NOT EXISTS (
                  SELECT 1 FROM accreditation_items WHERE competition_id = competitions.id
              )
        LOOP
            item_fut := gen_random_uuid();
            INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
            VALUES (item_fut, comp.org_id, comp.id, 'discipline', 'FUT', 'Fútbol', 0);

            item_vil := gen_random_uuid();
            INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
            VALUES (item_vil, comp.org_id, comp.id, 'venue', 'VIL', 'Villa / Concentración', 0);

            item_mpc := gen_random_uuid();
            INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
            VALUES (item_mpc, comp.org_id, comp.id, 'venue', 'MPC', 'Centro de Prensa', 1);

            item_est := gen_random_uuid();
            INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
            VALUES (item_est, comp.org_id, comp.id, 'venue', 'EST', 'Estadios de competencia', 2);

            item_ta := gen_random_uuid();
            INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
            VALUES (item_ta, comp.org_id, comp.id, 'service', 'TA', 'Transporte de atletas', 0);

            item_com := gen_random_uuid();
            INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
            VALUES (item_com, comp.org_id, comp.id, 'service', 'COM', 'Comedor', 1);

            item_blue := gen_random_uuid();
            INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, color_hex, display_order)
            VALUES (item_blue, comp.org_id, comp.id, 'zone', 'AZUL', 'Campo de juego y áreas operativas', '#1F3864', 0);

            item_prep := gen_random_uuid();
            INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
            VALUES (item_prep, comp.org_id, comp.id, 'zone', '2', 'Área de preparación de atletas', 1);

            item_res := gen_random_uuid();
            INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
            VALUES (item_res, comp.org_id, comp.id, 'zone', 'R', 'Zona residencial de la villa', 2);

            cat_aa := gen_random_uuid();
            INSERT INTO accreditation_categories (id, org_id, competition_id, code, name, color_hex, display_order)
            VALUES (cat_aa, comp.org_id, comp.id, 'Aa', 'Deportista', '#1F3864', 0);

            cat_ao := gen_random_uuid();
            INSERT INTO accreditation_categories (id, org_id, competition_id, code, name, color_hex, display_order)
            VALUES (cat_ao, comp.org_id, comp.id, 'Ao', 'Cuerpo técnico', '#6B2D90', 1);

            INSERT INTO accreditation_category_items (org_id, competition_id, category_id, item_id)
            SELECT comp.org_id, comp.id, cat.id, itm.id
            FROM (VALUES (cat_aa), (cat_ao)) AS cat(id)
            CROSS JOIN (VALUES
                (item_fut), (item_vil), (item_mpc), (item_est),
                (item_ta), (item_com),
                (item_blue), (item_prep), (item_res)
            ) AS itm(id);
        END LOOP;
    END LOOP;

    PERFORM set_config('app.current_org', '', true);
END $$;

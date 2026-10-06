-- =============================================================================
-- SeedFootballAccreditationCatalog
--
-- A starting accreditation catalogue for football competitions, so an
-- organizer does not open an empty screen and have to invent codes from
-- nothing.
--
-- The codes are not invented. They come from the Pan American Sports
-- Organization's Accreditation Users' Guide — the document that defines the
-- accreditation card operating system used across the Pan American, Bolivarian
-- and South American Games, which is the family of events this credential's
-- fixed structure was decreed from. Three things are taken from it as-is:
--
--   · The zone codes and what each one grants (§6.1, Accreditation Card
--     Operating System): "2" is the athletes' preparation area, "R" is the
--     residential zone of the village, and the colour band ("Blue") is field
--     of play plus operational and general circulation areas. A zone's colour
--     is not decoration — it is literally what the guide prints along the
--     foot of the card, which is why it is seeded with a colour and the other
--     kinds are not.
--   · The venue codes (§8, Venue access): VIL (village) and MPC (press
--     centre) are kept as published. IBC, the broadcast centre, is left out —
--     nothing in a domestic football competition broadcasts through one —
--     and EST (stadiums) is added in its place, naming what this sport
--     actually calls its competition venues.
--   · The transport and dining services: TA (athletes'/NOC transport system)
--     is the guide's own code. Dining has no PASO code — accreditation guides
--     draw it as a pictogram instead — so it is named plainly here.
--
--   · Two categories, from the guide's own population table for National
--     Olympic Committee entourages: Aa ("Pan Am athletes") and Ao ("coaches,
--     medical personnel, technical personnel, administrative personnel,
--     veterinarians"). Both carry the same package below, which matches what
--     the guide grants each of them — own sport venues, village, press
--     centre, zones 2 and R, Blue — because a team's technical staff needs
--     the same doors onto the field of play that an athlete does.
--
-- Scoped to football only, by design: sembrar un catálogo para un deporte que
-- nadie juega todavía en este sistema sería catálogo muerto, y el pedido fue
-- específicamente centrarse en fútbol.
--
-- Applied to every football competition that exists at the moment this
-- migration runs, and guarded per competition so re-running it is harmless.
-- It does NOT run again for a competition created afterwards — provisioning a
-- new competition's starting catalogue is an application action for a later
-- phase, not a database trigger fired behind the back of whoever is creating
-- a competition.
-- =============================================================================

DO $$
DECLARE
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
    FOR comp IN
        SELECT id, org_id
        FROM competitions
        WHERE sport_code = 'football'
          AND deleted_at IS NULL
          AND NOT EXISTS (
              SELECT 1 FROM accreditation_items WHERE competition_id = competitions.id
          )
    LOOP
        -- Discipline — one per card, the first box of the front's top row.
        item_fut := gen_random_uuid();
        INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
        VALUES (item_fut, comp.org_id, comp.id, 'discipline', 'FUT', 'Fútbol', 0);

        -- Venues — finish the top row.
        item_vil := gen_random_uuid();
        INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
        VALUES (item_vil, comp.org_id, comp.id, 'venue', 'VIL', 'Villa / Concentración', 0);

        item_mpc := gen_random_uuid();
        INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
        VALUES (item_mpc, comp.org_id, comp.id, 'venue', 'MPC', 'Centro de Prensa', 1);

        item_est := gen_random_uuid();
        INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
        VALUES (item_est, comp.org_id, comp.id, 'venue', 'EST', 'Estadios de competencia', 2);

        -- Services — the second row of boxes.
        item_ta := gen_random_uuid();
        INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
        VALUES (item_ta, comp.org_id, comp.id, 'service', 'TA', 'Transporte de atletas', 0);

        item_com := gen_random_uuid();
        INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
        VALUES (item_com, comp.org_id, comp.id, 'service', 'COM', 'Comedor', 1);

        -- Zones — the footer band, replicated on both faces. "Blue" carries a
        -- colour because that is what the guide prints: a colour strip, not a
        -- code in a box.
        item_blue := gen_random_uuid();
        INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, color_hex, display_order)
        VALUES (item_blue, comp.org_id, comp.id, 'zone', 'AZUL', 'Campo de juego y áreas operativas', '#1F3864', 0);

        item_prep := gen_random_uuid();
        INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
        VALUES (item_prep, comp.org_id, comp.id, 'zone', '2', 'Área de preparación de atletas', 1);

        item_res := gen_random_uuid();
        INSERT INTO accreditation_items (id, org_id, competition_id, kind, code, name, display_order)
        VALUES (item_res, comp.org_id, comp.id, 'zone', 'R', 'Zona residencial de la villa', 2);

        -- Categories — Aa and Ao from the guide's own NOC population table.
        cat_aa := gen_random_uuid();
        INSERT INTO accreditation_categories (id, org_id, competition_id, code, name, color_hex, display_order)
        VALUES (cat_aa, comp.org_id, comp.id, 'Aa', 'Deportista', '#1F3864', 0);

        cat_ao := gen_random_uuid();
        INSERT INTO accreditation_categories (id, org_id, competition_id, code, name, color_hex, display_order)
        VALUES (cat_ao, comp.org_id, comp.id, 'Ao', 'Cuerpo técnico', '#6B2D90', 1);

        -- Both categories carry the same package: the guide grants Aa and Ao
        -- the same venue and zone access (own sport venues, village, press
        -- centre, zones 2 and R, Blue) — a team's technical staff needs the
        -- same doors onto the field of play that an athlete does.
        INSERT INTO accreditation_category_items (org_id, competition_id, category_id, item_id)
        SELECT comp.org_id, comp.id, cat.id, itm.id
        FROM (VALUES (cat_aa), (cat_ao)) AS cat(id)
        CROSS JOIN (VALUES
            (item_fut), (item_vil), (item_mpc), (item_est),
            (item_ta), (item_com),
            (item_blue), (item_prep), (item_res)
        ) AS itm(id);
    END LOOP;
END $$;

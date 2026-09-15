-- =============================================================================
-- SeedRealChampionshipsData.Up.sql
--
-- Inserta datos completos y reales de prueba para dos campeonatos:
-- 1. Fútbol: "Copa Liga Profesional Bolivia 2026" (Fútbol 11 - Grupos y Llaves)
-- 2. Taekwondo: "Gran Campeonato Nacional de Taekwondo 2026" (Kyorugi & Poomsae)
--
-- Inclusión de inscripciones en nómina (roster_entries) para competidores de Taekwondo.
-- =============================================================================

DO $$
DECLARE
    org_rec RECORD;
    v_user_id uuid;
    
    -- IDs para Fútbol
    v_ruleset_fb_id uuid;
    v_venue_fb_id uuid;
    v_space_fb_id uuid;
    v_comp_fb_id uuid;
    v_cat_fb_id uuid;
    
    v_club_bol_id uuid;
    v_club_str_id uuid;
    v_club_wil_id uuid;
    v_club_ori_id uuid;
    
    v_team_bol_id uuid;
    v_team_str_id uuid;
    v_team_wil_id uuid;
    v_team_ori_id uuid;
    
    v_ath_bol_1 uuid; v_ath_bol_2 uuid; v_ath_bol_3 uuid;
    v_ath_str_1 uuid; v_ath_str_2 uuid; v_ath_str_3 uuid;

    v_rost_bol_1 uuid; v_rost_bol_2 uuid; v_rost_bol_3 uuid;
    v_rost_str_1 uuid; v_rost_str_2 uuid; v_rost_str_3 uuid;
    
    v_match_fb_1 uuid; v_match_fb_2 uuid; v_match_fb_3 uuid;
    v_metric_goal uuid; v_metric_yellow uuid; v_metric_assist uuid;
    
    -- IDs para Taekwondo
    v_ruleset_tkd_id uuid;
    v_venue_tkd_id uuid;
    v_space_tkd_1_id uuid; v_space_tkd_2_id uuid;
    v_comp_tkd_id uuid;
    v_cat_tkd_m_id uuid; v_cat_tkd_f_id uuid;
    
    v_club_dojang1 uuid; v_club_dojang2 uuid; v_club_dojang3 uuid;
    
    v_team_tkd_m1 uuid; v_team_tkd_m2 uuid; v_team_tkd_m3 uuid; v_team_tkd_m4 uuid;
    v_team_tkd_f1 uuid; v_team_tkd_f2 uuid;
    
    v_ath_tkd_m1 uuid; v_ath_tkd_m2 uuid; v_ath_tkd_m3 uuid; v_ath_tkd_m4 uuid;
    v_ath_tkd_f1 uuid; v_ath_tkd_f2 uuid;

    v_rost_tkd_m1 uuid; v_rost_tkd_m2 uuid; v_rost_tkd_m3 uuid; v_rost_tkd_m4 uuid;
    v_rost_tkd_f1 uuid; v_rost_tkd_f2 uuid;
    
    v_match_tkd_1 uuid; v_match_tkd_2 uuid; v_match_tkd_3 uuid;

BEGIN
    -- Obtener un ID de usuario registrado para recorded_by
    SELECT id INTO v_user_id FROM users LIMIT 1;

    FOR org_rec IN SELECT id FROM organizations LOOP
        -- Establecer el contexto RLS para la organización actual
        PERFORM set_config('app.current_org', org_rec.id::text, true);
        
        -- Limpiar registros asociados en orden inverso de claves foráneas
        DELETE FROM player_events WHERE match_id IN (
            SELECT m.id FROM matches m JOIN competitions c ON m.competition_id = c.id
            WHERE c.org_id = org_rec.id AND c.slug IN ('liga-profesional-2026', 'nacional-taekwondo-2026')
        );
        DELETE FROM matches WHERE competition_id IN (
            SELECT id FROM competitions WHERE org_id = org_rec.id AND slug IN ('liga-profesional-2026', 'nacional-taekwondo-2026')
        );
        DELETE FROM roster_entries WHERE team_id IN (
            SELECT t.id FROM teams t JOIN categories c ON t.category_id = c.id JOIN competitions comp ON c.competition_id = comp.id
            WHERE comp.org_id = org_rec.id AND comp.slug IN ('liga-profesional-2026', 'nacional-taekwondo-2026')
        );
        DELETE FROM teams WHERE category_id IN (
            SELECT c.id FROM categories c JOIN competitions comp ON c.competition_id = comp.id
            WHERE comp.org_id = org_rec.id AND comp.slug IN ('liga-profesional-2026', 'nacional-taekwondo-2026')
        );
        DELETE FROM categories WHERE competition_id IN (
            SELECT id FROM competitions WHERE org_id = org_rec.id AND slug IN ('liga-profesional-2026', 'nacional-taekwondo-2026')
        );
        DELETE FROM competitions WHERE org_id = org_rec.id AND slug IN ('liga-profesional-2026', 'nacional-taekwondo-2026');

        -- ---------------------------------------------------------------------
        -- 1. REGLAMENTOS (RULESETS)
        -- ---------------------------------------------------------------------
        
        v_ruleset_fb_id := gen_random_uuid();
        INSERT INTO rulesets (id, org_id, sport_code, name, config) VALUES (
            v_ruleset_fb_id, org_rec.id, 'football', 'Reglamento Oficial FPF - Fútbol 11 (2026)',
            '{
                "periods": { "count": 2, "label": "tiempo", "minutes": 45 },
                "points": { "win": 3, "draw": 1, "loss": 0 },
                "tiebreakers": ["score_difference", "score_for", "head_to_head"],
                "walkover": { "winnerScore": 3, "loserScore": 0 },
                "metrics": ["goal", "own_goal", "assist", "yellow_card", "red_card"]
            }'::jsonb
        ) ON CONFLICT (org_id, name) DO UPDATE SET config = EXCLUDED.config RETURNING id INTO v_ruleset_fb_id;

        v_ruleset_tkd_id := gen_random_uuid();
        INSERT INTO rulesets (id, org_id, sport_code, name, config) VALUES (
            v_ruleset_tkd_id, org_rec.id, 'taekwondo_kyorugi', 'Reglamento Oficial WT - Kyorugi Best of 3',
            '{
                "periods": { "count": 3, "label": "asalto", "minutes": 2 },
                "points": { "win_3_0": 3, "win_2_1": 2, "win_2_0": 3, "loss_1_2": 1, "loss_0_2": 0 },
                "tiebreakers": ["score_difference", "score_for"],
                "walkover": null,
                "metrics": ["point", "penalty"]
            }'::jsonb
        ) ON CONFLICT (org_id, name) DO UPDATE SET config = EXCLUDED.config RETURNING id INTO v_ruleset_tkd_id;

        -- ---------------------------------------------------------------------
        -- 2. SEDES Y ESPACIOS (VENUES & VENUE_SPACES)
        -- ---------------------------------------------------------------------
        
        v_venue_fb_id := gen_random_uuid();
        INSERT INTO venues (id, org_id, name, address, is_active) VALUES
            (v_venue_fb_id, org_rec.id, 'Estadio Olímpico Hernando Siles', 'Av. Saavedra 1120, La Paz', true)
        ON CONFLICT (org_id, name) DO UPDATE SET is_active = true RETURNING id INTO v_venue_fb_id;

        v_space_fb_id := gen_random_uuid();
        INSERT INTO venue_spaces (id, org_id, venue_id, name, is_active) VALUES
            (v_space_fb_id, org_rec.id, v_venue_fb_id, 'Cancha Principal (Césped Natural)', true)
        ON CONFLICT (venue_id, name) DO UPDATE SET is_active = true RETURNING id INTO v_space_fb_id;

        v_venue_tkd_id := gen_random_uuid();
        INSERT INTO venues (id, org_id, name, address, is_active) VALUES
            (v_venue_tkd_id, org_rec.id, 'Coliseo Cerrado Julio Borelli Viteritto', 'Calle México esq. Cañada Strongest, La Paz', true)
        ON CONFLICT (org_id, name) DO UPDATE SET is_active = true RETURNING id INTO v_venue_tkd_id;

        v_space_tkd_1_id := gen_random_uuid();
        INSERT INTO venue_spaces (id, org_id, venue_id, name, is_active) VALUES
            (v_space_tkd_1_id, org_rec.id, v_venue_tkd_id, 'Tapiz Central A (Combate)', true)
        ON CONFLICT (venue_id, name) DO UPDATE SET is_active = true RETURNING id INTO v_space_tkd_1_id;

        v_space_tkd_2_id := gen_random_uuid();
        INSERT INTO venue_spaces (id, org_id, venue_id, name, is_active) VALUES
            (v_space_tkd_2_id, org_rec.id, v_venue_tkd_id, 'Tapiz B (Poomsae / Calentamiento)', true)
        ON CONFLICT (venue_id, name) DO UPDATE SET is_active = true RETURNING id INTO v_space_tkd_2_id;

        -- ---------------------------------------------------------------------
        -- 3. CLUBES
        -- ---------------------------------------------------------------------
        
        v_club_bol_id := gen_random_uuid();
        INSERT INTO clubs (id, org_id, name, short_name, logo_url, is_active) VALUES
            (v_club_bol_id, org_rec.id, 'Club Bolívar', 'Bolívar', 'https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=150', true)
        ON CONFLICT (org_id, name) WHERE deleted_at IS NULL DO UPDATE SET is_active = true RETURNING id INTO v_club_bol_id;

        v_club_str_id := gen_random_uuid();
        INSERT INTO clubs (id, org_id, name, short_name, logo_url, is_active) VALUES
            (v_club_str_id, org_rec.id, 'Club The Strongest', 'Strongest', 'https://images.unsplash.com/photo-1579952363873-27f3bade9f55?w=150', true)
        ON CONFLICT (org_id, name) WHERE deleted_at IS NULL DO UPDATE SET is_active = true RETURNING id INTO v_club_str_id;

        v_club_wil_id := gen_random_uuid();
        INSERT INTO clubs (id, org_id, name, short_name, logo_url, is_active) VALUES
            (v_club_wil_id, org_rec.id, 'Club Jorge Wilstermann', 'Wilstermann', 'https://images.unsplash.com/photo-1518091043644-c1d4457512c6?w=150', true)
        ON CONFLICT (org_id, name) WHERE deleted_at IS NULL DO UPDATE SET is_active = true RETURNING id INTO v_club_wil_id;

        v_club_ori_id := gen_random_uuid();
        INSERT INTO clubs (id, org_id, name, short_name, logo_url, is_active) VALUES
            (v_club_ori_id, org_rec.id, 'Club Oriente Petrolero', 'Oriente', 'https://images.unsplash.com/photo-1574629810360-7efbbe195018?w=150', true)
        ON CONFLICT (org_id, name) WHERE deleted_at IS NULL DO UPDATE SET is_active = true RETURNING id INTO v_club_ori_id;

        v_club_dojang1 := gen_random_uuid();
        INSERT INTO clubs (id, org_id, name, short_name, logo_url, is_active) VALUES
            (v_club_dojang1, org_rec.id, 'Do-Jang Dragones Rojos TKD', 'Dragones', 'https://images.unsplash.com/photo-1517838277536-f5f99be501cd?w=150', true)
        ON CONFLICT (org_id, name) WHERE deleted_at IS NULL DO UPDATE SET is_active = true RETURNING id INTO v_club_dojang1;

        v_club_dojang2 := gen_random_uuid();
        INSERT INTO clubs (id, org_id, name, short_name, logo_url, is_active) VALUES
            (v_club_dojang2, org_rec.id, 'Academia Cobra Martial Arts', 'Cobra TKD', 'https://images.unsplash.com/photo-1555597673-b21d5c935865?w=150', true)
        ON CONFLICT (org_id, name) WHERE deleted_at IS NULL DO UPDATE SET is_active = true RETURNING id INTO v_club_dojang2;

        v_club_dojang3 := gen_random_uuid();
        INSERT INTO clubs (id, org_id, name, short_name, logo_url, is_active) VALUES
            (v_club_dojang3, org_rec.id, 'Club Titanes del Altiplano', 'Titanes', 'https://images.unsplash.com/photo-1544367567-0f2fcb009e0b?w=150', true)
        ON CONFLICT (org_id, name) WHERE deleted_at IS NULL DO UPDATE SET is_active = true RETURNING id INTO v_club_dojang3;

        -- ---------------------------------------------------------------------
        -- 4. CAMPEONATO 1: FÚTBOL (LIGA PROFESIONAL BOLIVIA 2026)
        -- ---------------------------------------------------------------------
        v_comp_fb_id := gen_random_uuid();
        INSERT INTO competitions (
            id, org_id, sport_code, ruleset_id, name, slug, season, format,
            capture_level, status, starts_on, ends_on, is_public, settings
        ) VALUES (
            v_comp_fb_id, org_rec.id, 'football', v_ruleset_fb_id,
            'Copa Liga Profesional Bolivia 2026', 'liga-profesional-2026', '2026', 'hybrid',
            'detailed', 'in_progress', '2026-03-01', '2026-11-30', true,
            '{
                "public": {
                    "accentColor": "#1B8A2E",
                    "show_standings": true,
                    "show_leaders": true,
                    "show_rosters": true,
                    "credentialRules": [
                        "Credencial oficial de la Liga Profesional de Fútbol.",
                        "Obligatorio presentar junto al documento nacional de identidad.",
                        "Válida para el ingreso a zonas de vestuarios y terreno de juego."
                    ],
                    "sponsors": [
                        { "name": "Puma", "url": "https://puma.com", "logo_key": "sponsors/puma.png" },
                        { "name": "Gatorade", "url": "https://gatorade.com", "logo_key": "sponsors/gatorade.png" },
                        { "name": "Entel Bolivia", "url": "https://entel.bo", "logo_key": "sponsors/entel.png" },
                        { "name": "Banco Nacional de Bolivia", "url": "https://bnb.com.bo", "logo_key": "sponsors/bnb.png" }
                    ]
                },
                "publicPreview": {
                    "bannerUrl": "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=1200&h=400&fit=crop"
                }
            }'::jsonb
        );

        -- Categoría de Fútbol
        v_cat_fb_id := gen_random_uuid();
        INSERT INTO categories (id, org_id, competition_id, ruleset_id, name, gender, max_roster_size, display_order) VALUES
            (v_cat_fb_id, org_rec.id, v_comp_fb_id, v_ruleset_fb_id, 'Primera División Masculina', 'M', 30, 1)
        ON CONFLICT (competition_id, name) DO UPDATE SET max_roster_size = 30 RETURNING id INTO v_cat_fb_id;

        -- Equipos de Fútbol
        v_team_bol_id := gen_random_uuid();
        INSERT INTO teams (id, org_id, club_id, category_id, name, group_label, is_individual) VALUES
            (v_team_bol_id, org_rec.id, v_club_bol_id, v_cat_fb_id, 'Bolívar - Primera A', 'A', false)
        ON CONFLICT (category_id, club_id) WHERE deleted_at IS NULL AND NOT is_individual DO UPDATE SET group_label = 'A' RETURNING id INTO v_team_bol_id;

        v_team_str_id := gen_random_uuid();
        INSERT INTO teams (id, org_id, club_id, category_id, name, group_label, is_individual) VALUES
            (v_team_str_id, org_rec.id, v_club_str_id, v_cat_fb_id, 'The Strongest - Primera A', 'A', false)
        ON CONFLICT (category_id, club_id) WHERE deleted_at IS NULL AND NOT is_individual DO UPDATE SET group_label = 'A' RETURNING id INTO v_team_str_id;

        v_team_wil_id := gen_random_uuid();
        INSERT INTO teams (id, org_id, club_id, category_id, name, group_label, is_individual) VALUES
            (v_team_wil_id, org_rec.id, v_club_wil_id, v_cat_fb_id, 'Wilstermann - Primera A', 'B', false)
        ON CONFLICT (category_id, club_id) WHERE deleted_at IS NULL AND NOT is_individual DO UPDATE SET group_label = 'B' RETURNING id INTO v_team_wil_id;

        v_team_ori_id := gen_random_uuid();
        INSERT INTO teams (id, org_id, club_id, category_id, name, group_label, is_individual) VALUES
            (v_team_ori_id, org_rec.id, v_club_ori_id, v_cat_fb_id, 'Oriente Petrolero - Primera A', 'B', false)
        ON CONFLICT (category_id, club_id) WHERE deleted_at IS NULL AND NOT is_individual DO UPDATE SET group_label = 'B' RETURNING id INTO v_team_ori_id;

        -- Atletas de Fútbol
        v_ath_bol_1 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, photo_key)
        VALUES (v_ath_bol_1, org_rec.id, 'Carlos', 'Lampe', '4829103-LP', '1987-03-17', 'M', 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_bol_1;

        v_ath_bol_2 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, photo_key)
        VALUES (v_ath_bol_2, org_rec.id, 'Patito', 'Rodríguez', '8392014-LP', '1990-08-23', 'M', 'https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_bol_2;

        v_ath_bol_3 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, photo_key)
        VALUES (v_ath_bol_3, org_rec.id, 'Ramiro', 'Vaca', '9102938-TJA', '1999-07-01', 'M', 'https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_bol_3;

        v_ath_str_1 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, photo_key)
        VALUES (v_ath_str_1, org_rec.id, 'Guillermo', 'Viscarra', '6749201-SC', '1993-02-07', 'M', 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_str_1;

        v_ath_str_2 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, photo_key)
        VALUES (v_ath_str_2, org_rec.id, 'Michael', 'Ortega', '5930291-EXT', '1991-04-06', 'M', 'https://images.unsplash.com/photo-1501196354995-cbb51c65aaea?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_str_2;

        v_ath_str_3 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, photo_key)
        VALUES (v_ath_str_3, org_rec.id, 'Enrique', 'Triverio', '7729104-EXT', '1988-12-31', 'M', 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_str_3;

        -- Nómina de Fútbol
        v_rost_bol_1 := gen_random_uuid(); v_rost_bol_2 := gen_random_uuid(); v_rost_bol_3 := gen_random_uuid();
        v_rost_str_1 := gen_random_uuid(); v_rost_str_2 := gen_random_uuid(); v_rost_str_3 := gen_random_uuid();

        INSERT INTO roster_entries (id, org_id, team_id, athlete_id, jersey_number, position) VALUES
            (v_rost_bol_1, org_rec.id, v_team_bol_id, v_ath_bol_1, 1, 'goalkeeper'),
            (v_rost_bol_2, org_rec.id, v_team_bol_id, v_ath_bol_2, 10, 'captain'),
            (v_rost_bol_3, org_rec.id, v_team_bol_id, v_ath_bol_3, 8, 'player'),
            (v_rost_str_1, org_rec.id, v_team_str_id, v_ath_str_1, 1, 'goalkeeper'),
            (v_rost_str_2, org_rec.id, v_team_str_id, v_ath_str_2, 10, 'captain'),
            (v_rost_str_3, org_rec.id, v_team_str_id, v_ath_str_3, 9, 'player')
        ON CONFLICT DO NOTHING;

        -- Partidos de Fútbol
        v_match_fb_1 := gen_random_uuid(); v_match_fb_2 := gen_random_uuid(); v_match_fb_3 := gen_random_uuid();

        INSERT INTO matches (
            id, org_id, competition_id, category_id, home_team_id, away_team_id, venue_space_id,
            round_number, phase, scheduled_at, status, period_scores, home_total, away_total, recorded_by, notes
        ) VALUES (
            v_match_fb_1, org_rec.id, v_comp_fb_id, v_cat_fb_id, v_team_bol_id, v_team_str_id, v_space_fb_id,
            1, null, '2026-04-15 15:30:00+00', 'finished',
            '[{"p":1,"h":1,"a":0},{"p":2,"h":1,"a":1}]'::jsonb, 2, 1, v_user_id, 'Clásico Paceño - Gran asistencia de público.'
        );

        INSERT INTO matches (
            id, org_id, competition_id, category_id, home_team_id, away_team_id, venue_space_id,
            round_number, phase, scheduled_at, status, period_scores, home_total, away_total, recorded_by, notes
        ) VALUES (
            v_match_fb_2, org_rec.id, v_comp_fb_id, v_cat_fb_id, v_team_wil_id, v_team_ori_id, v_space_fb_id,
            1, null, '2026-04-16 18:00:00+00', 'finished',
            '[{"p":1,"h":2,"a":0},{"p":2,"h":1,"a":0}]'::jsonb, 3, 0, v_user_id, 'Victoria contundente del local.'
        );

        INSERT INTO matches (
            id, org_id, competition_id, category_id, home_team_id, away_team_id, venue_space_id,
            round_number, phase, scheduled_at, status, period_scores, home_total, away_total, recorded_by, notes
        ) VALUES (
            v_match_fb_3, org_rec.id, v_comp_fb_id, v_cat_fb_id, v_team_bol_id, v_team_wil_id, v_space_fb_id,
            null, 'final', '2026-11-28 20:00:00+00', 'scheduled',
            null, null, null, null, 'Gran Final de la Copa Liga Profesional.'
        );

        -- Eventos del Partido 1
        SELECT id INTO v_metric_goal FROM sport_metrics WHERE sport_code = 'football' AND code = 'goal' LIMIT 1;

        IF v_metric_goal IS NOT NULL THEN
            INSERT INTO player_events (org_id, match_id, roster_entry_id, metric_id, period_number, minute, quantity) VALUES
                (org_rec.id, v_match_fb_1, v_rost_bol_2, v_metric_goal, 1, 24, 1),
                (org_rec.id, v_match_fb_1, v_rost_str_3, v_metric_goal, 2, 60, 1),
                (org_rec.id, v_match_fb_1, v_rost_bol_3, v_metric_goal, 2, 88, 1);
        END IF;

        -- ---------------------------------------------------------------------
        -- 5. CAMPEONATO 2: TAEKWONDO (GRAN ABIERTO NACIONAL TKD 2026)
        -- ---------------------------------------------------------------------
        v_comp_tkd_id := gen_random_uuid();
        INSERT INTO competitions (
            id, org_id, sport_code, ruleset_id, name, slug, season, format,
            capture_level, status, starts_on, ends_on, is_public, settings
        ) VALUES (
            v_comp_tkd_id, org_rec.id, 'taekwondo_kyorugi', v_ruleset_tkd_id,
            'Gran Campeonato Nacional de Taekwondo Kyorugi & Poomsae 2026', 'nacional-taekwondo-2026', '2026', 'knockout',
            'detailed', 'in_progress', '2026-05-10', '2026-05-12', true,
            '{
                "public": {
                    "accentColor": "#D32F2F",
                    "show_standings": true,
                    "show_leaders": true,
                    "show_rosters": true,
                    "credentialRules": [
                        "Credencial Oficial de Combate Taekwondo WT.",
                        "Requisito pesaje oficial previo al combate.",
                        "Acceso autorizado únicamente con protector de cabezal y bucal."
                    ],
                    "sponsors": [
                        { "name": "Daedo International", "url": "https://daedo.com", "logo_key": "sponsors/daedo.png" },
                        { "name": "Adidas Martial Arts", "url": "https://adidas.com", "logo_key": "sponsors/adidas.png" },
                        { "name": "Kwon Combat", "url": "https://kwon.com", "logo_key": "sponsors/kwon.png" }
                    ]
                },
                "publicPreview": {
                    "bannerUrl": "https://images.unsplash.com/photo-1555597673-b21d5c935865?w=1200&h=400&fit=crop"
                }
            }'::jsonb
        );

        -- Categorías de Taekwondo
        v_cat_tkd_m_id := gen_random_uuid();
        INSERT INTO categories (id, org_id, competition_id, ruleset_id, name, gender, min_weight_kg, max_weight_kg, max_roster_size, display_order) VALUES
            (v_cat_tkd_m_id, org_rec.id, v_comp_tkd_id, v_ruleset_tkd_id, 'Kyorugi Senior Masculino (-68kg)', 'M', 63.00, 68.00, 1, 1)
        ON CONFLICT (competition_id, name) DO UPDATE SET min_weight_kg = 63.00, max_weight_kg = 68.00 RETURNING id INTO v_cat_tkd_m_id;

        v_cat_tkd_f_id := gen_random_uuid();
        INSERT INTO categories (id, org_id, competition_id, ruleset_id, name, gender, min_weight_kg, max_weight_kg, max_roster_size, display_order) VALUES
            (v_cat_tkd_f_id, org_rec.id, v_comp_tkd_id, v_ruleset_tkd_id, 'Kyorugi Senior Femenino (-57kg)', 'F', 53.00, 57.00, 1, 2)
        ON CONFLICT (competition_id, name) DO UPDATE SET min_weight_kg = 53.00, max_weight_kg = 57.00 RETURNING id INTO v_cat_tkd_f_id;

        -- Equipos/Entrantes individuales de Taekwondo Masculino
        v_team_tkd_m1 := gen_random_uuid();
        INSERT INTO teams (id, org_id, club_id, category_id, name, is_individual)
        VALUES (v_team_tkd_m1, org_rec.id, v_club_dojang1, v_cat_tkd_m_id, 'Mateo Flores (Dragones)', true);

        v_team_tkd_m2 := gen_random_uuid();
        INSERT INTO teams (id, org_id, club_id, category_id, name, is_individual)
        VALUES (v_team_tkd_m2, org_rec.id, v_club_dojang2, v_cat_tkd_m_id, 'Sebastián Choque (Cobra TKD)', true);

        v_team_tkd_m3 := gen_random_uuid();
        INSERT INTO teams (id, org_id, club_id, category_id, name, is_individual)
        VALUES (v_team_tkd_m3, org_rec.id, v_club_dojang3, v_cat_tkd_m_id, 'Kevin Mendoza (Titanes)', true);

        v_team_tkd_m4 := gen_random_uuid();
        INSERT INTO teams (id, org_id, club_id, category_id, name, is_individual)
        VALUES (v_team_tkd_m4, org_rec.id, v_club_dojang1, v_cat_tkd_m_id, 'Lucas Gutiérrez (Dragones)', true);

        -- Equipos/Entrantes individuales de Taekwondo Femenino
        v_team_tkd_f1 := gen_random_uuid();
        INSERT INTO teams (id, org_id, club_id, category_id, name, is_individual)
        VALUES (v_team_tkd_f1, org_rec.id, v_club_dojang1, v_cat_tkd_f_id, 'Camila Morales (Dragones)', true);

        v_team_tkd_f2 := gen_random_uuid();
        INSERT INTO teams (id, org_id, club_id, category_id, name, is_individual)
        VALUES (v_team_tkd_f2, org_rec.id, v_club_dojang2, v_cat_tkd_f_id, 'Valentina Ortiz (Cobra TKD)', true);

        -- Atletas de Taekwondo Masculino
        v_ath_tkd_m1 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, weight_kg, photo_key)
        VALUES (v_ath_tkd_m1, org_rec.id, 'Mateo', 'Flores', '9403921-CB', '2001-05-12', 'M', 66.50, 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_tkd_m1;

        v_ath_tkd_m2 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, weight_kg, photo_key)
        VALUES (v_ath_tkd_m2, org_rec.id, 'Sebastián', 'Choque', '8493021-LP', '2002-09-18', 'M', 67.20, 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_tkd_m2;

        v_ath_tkd_m3 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, weight_kg, photo_key)
        VALUES (v_ath_tkd_m3, org_rec.id, 'Kevin', 'Mendoza', '7392019-OR', '2000-11-04', 'M', 65.80, 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_tkd_m3;

        v_ath_tkd_m4 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, weight_kg, photo_key)
        VALUES (v_ath_tkd_m4, org_rec.id, 'Lucas', 'Gutiérrez', '6291038-SC', '2003-01-25', 'M', 67.80, 'https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_tkd_m4;

        -- Atletas de Taekwondo Femenino
        v_ath_tkd_f1 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, weight_kg, photo_key)
        VALUES (v_ath_tkd_f1, org_rec.id, 'Camila', 'Morales', '5192048-LP', '2003-07-14', 'F', 55.40, 'https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_tkd_f1;

        v_ath_tkd_f2 := gen_random_uuid();
        INSERT INTO athletes (id, org_id, first_name, last_name, document_id, birth_date, gender, weight_kg, photo_key)
        VALUES (v_ath_tkd_f2, org_rec.id, 'Valentina', 'Ortiz', '4092817-CB', '2004-03-22', 'F', 56.10, 'https://images.unsplash.com/photo-1517841905240-472988babdf9?w=200')
        ON CONFLICT (org_id, document_id) WHERE deleted_at IS NULL DO UPDATE SET first_name = EXCLUDED.first_name RETURNING id INTO v_ath_tkd_f2;

        -- Nóminas de Taekwondo (asociando competidores individuales a sus equipos)
        v_rost_tkd_m1 := gen_random_uuid(); v_rost_tkd_m2 := gen_random_uuid();
        v_rost_tkd_m3 := gen_random_uuid(); v_rost_tkd_m4 := gen_random_uuid();
        v_rost_tkd_f1 := gen_random_uuid(); v_rost_tkd_f2 := gen_random_uuid();

        INSERT INTO roster_entries (id, org_id, team_id, athlete_id) VALUES
            (v_rost_tkd_m1, org_rec.id, v_team_tkd_m1, v_ath_tkd_m1),
            (v_rost_tkd_m2, org_rec.id, v_team_tkd_m2, v_ath_tkd_m2),
            (v_rost_tkd_m3, org_rec.id, v_team_tkd_m3, v_ath_tkd_m3),
            (v_rost_tkd_m4, org_rec.id, v_team_tkd_m4, v_ath_tkd_m4),
            (v_rost_tkd_f1, org_rec.id, v_team_tkd_f1, v_ath_tkd_f1),
            (v_rost_tkd_f2, org_rec.id, v_team_tkd_f2, v_ath_tkd_f2)
        ON CONFLICT DO NOTHING;

        -- Combates / Partidos de Eliminatoria Taekwondo
        v_match_tkd_1 := gen_random_uuid(); v_match_tkd_2 := gen_random_uuid(); v_match_tkd_3 := gen_random_uuid();

        INSERT INTO matches (
            id, org_id, competition_id, category_id, home_team_id, away_team_id, venue_space_id,
            phase, scheduled_at, status, period_scores, home_total, away_total, recorded_by, notes
        ) VALUES (
            v_match_tkd_1, org_rec.id, v_comp_tkd_id, v_cat_tkd_m_id, v_team_tkd_m1, v_team_tkd_m2, v_space_tkd_1_id,
            'semi_final', '2026-05-11 10:00:00+00', 'finished',
            '[{"p":1,"h":8,"a":3},{"p":2,"h":12,"a":5}]'::jsonb, 2, 0, v_user_id, 'Semifinal 1 Senior Male -68kg.'
        );

        INSERT INTO matches (
            id, org_id, competition_id, category_id, home_team_id, away_team_id, venue_space_id,
            phase, scheduled_at, status, period_scores, home_total, away_total, recorded_by, notes
        ) VALUES (
            v_match_tkd_2, org_rec.id, v_comp_tkd_id, v_cat_tkd_m_id, v_team_tkd_m3, v_team_tkd_m4, v_space_tkd_1_id,
            'semi_final', '2026-05-11 11:00:00+00', 'finished',
            '[{"p":1,"h":5,"a":7},{"p":2,"h":9,"a":4},{"p":3,"h":11,"a":8}]'::jsonb, 2, 1, v_user_id, 'Semifinal 2 Senior Male -68kg.'
        );

        INSERT INTO matches (
            id, org_id, competition_id, category_id, home_team_id, away_team_id, venue_space_id,
            phase, scheduled_at, status, period_scores, home_total, away_total, recorded_by, notes
        ) VALUES (
            v_match_tkd_3, org_rec.id, v_comp_tkd_id, v_cat_tkd_m_id, v_team_tkd_m1, v_team_tkd_m3, v_space_tkd_1_id,
            'final', '2026-05-12 16:00:00+00', 'scheduled',
            null, null, null, null, 'Combate por la Medalla de Oro Senior Male -68kg.'
        );

    END LOOP;
END $$;

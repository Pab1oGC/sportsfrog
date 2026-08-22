-- =============================================================================
-- SPORTFROG — Bootstrap sports catalog
--
-- Content shared by every organization, with no org_id and therefore outside
-- the isolation policies. Managed through migrations, not through the UI.
--
-- Display values (sport and metric names below) stay in Spanish: they're
-- end-user-facing business data for Spanish-speaking sports organizations,
-- not source code.
--
-- For sports with score_mode = 'sets', no metric carries affects_score: the
-- match result is sets won, consolidated from period_scores rather than from
-- summing events.
-- =============================================================================

INSERT INTO sports (code, name, period_label, default_periods, scoring_unit, score_mode) VALUES
    ('football',   'Fútbol',      'tiempo', 2, 'gol',   'cumulative'),
    ('futsal',     'Futsal',      'tiempo', 2, 'gol',   'cumulative'),
    ('basketball', 'Básquetbol',  'cuarto', 4, 'punto', 'cumulative'),
    ('volleyball', 'Vóleibol',    'set',    5, 'punto', 'sets'),
    ('wally',      'Wally',       'set',    3, 'punto', 'sets');

INSERT INTO sport_metrics (sport_code, code, label, affects_score, is_rankable, display_order) VALUES
    -- Football
    ('football',   'goal',         'Gol',              true,  true,  1),
    ('football',   'own_goal',     'Gol en contra',    true,  false, 2),
    ('football',   'assist',       'Asistencia',       false, true,  3),
    ('football',   'yellow_card',  'Tarjeta amarilla', false, true,  4),
    ('football',   'red_card',     'Tarjeta roja',     false, true,  5),

    -- Futsal
    ('futsal',     'goal',         'Gol',              true,  true,  1),
    ('futsal',     'own_goal',     'Gol en contra',    true,  false, 2),
    ('futsal',     'assist',       'Asistencia',       false, true,  3),
    ('futsal',     'yellow_card',  'Tarjeta amarilla', false, true,  4),
    ('futsal',     'red_card',     'Tarjeta roja',     false, true,  5),

    -- Basketball
    ('basketball', 'free_throw',   'Tiro libre',       true,  true,  1),
    ('basketball', 'field_goal',   'Doble',            true,  true,  2),
    ('basketball', 'three_point',  'Triple',           true,  true,  3),
    ('basketball', 'rebound',      'Rebote',           false, true,  4),
    ('basketball', 'assist',       'Asistencia',       false, true,  5),
    ('basketball', 'foul',         'Falta',            false, true,  6),

    -- Volleyball
    ('volleyball', 'point',        'Punto',            false, true,  1),
    ('volleyball', 'ace',          'Ace',              false, true,  2),
    ('volleyball', 'block',        'Bloqueo',          false, true,  3),
    ('volleyball', 'error',        'Error',            false, true,  4),

    -- Wally
    ('wally',      'point',        'Punto',            false, true,  1),
    ('wally',      'ace',          'Ace',              false, true,  2),
    ('wally',      'block',        'Bloqueo',          false, true,  3),
    ('wally',      'error',        'Error',            false, true,  4);

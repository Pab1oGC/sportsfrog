-- =============================================================================
-- SeedPoomsaeCatalog
--
-- Poomsae's bracket bouts: one performance a side, scored directly by
-- judges rather than built from summed events or periods won. score_mode
-- 'judged' was made a legal value back in AddTaekwondoCatalogFoundations;
-- this is the first row to actually use it, now that the domain has
-- ScoreMode.Judged and its rules to back it.
--
-- No sport_metrics: nothing about a judged score is an event to record
-- individually the way a goal or a block is, so there is nothing to seed —
-- exactly like wally and taekwondo_kyorugi under score_mode = 'sets',
-- neither of which prices a metric against the score either.
-- =============================================================================

INSERT INTO sports (code, name, period_label, default_periods, scoring_unit, score_mode, is_individual) VALUES
    ('taekwondo_poomsae', 'Taekwondo (Poomsae)', 'actuación', 1, 'punto', 'judged', true);

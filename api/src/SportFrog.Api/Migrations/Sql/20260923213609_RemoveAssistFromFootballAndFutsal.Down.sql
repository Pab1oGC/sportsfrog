-- =============================================================================
-- RemoveAssistFromFootballAndFutsal - rollback
--
-- Puts the metric back in the catalog, in its original place. It does not put
-- "assist" back into the rulesets that listed it: which ones did is not
-- recorded anywhere once the Up has run, and a ruleset without an explicit
-- metric list already means "every metric the sport offers", so a ruleset
-- that had a custom list simply stays as it is.
-- =============================================================================

UPDATE sport_metrics SET display_order = 4
 WHERE sport_code IN ('football', 'futsal') AND code = 'yellow_card';

UPDATE sport_metrics SET display_order = 5
 WHERE sport_code IN ('football', 'futsal') AND code = 'red_card';

INSERT INTO sport_metrics (sport_code, code, label, affects_score, is_rankable, display_order) VALUES
    ('football', 'assist', 'Asistencia', false, true, 3),
    ('futsal',   'assist', 'Asistencia', false, true, 3);

-- =============================================================================
-- AddVolleyballScoringMetrics (Down)
--
-- Mirrors the Up migration statement by statement, in reverse, back to the
-- shape SeedCatalog originally seeded: point, ace, block and error all
-- affects_score = false; a single "block" metric; error rankable again.
-- =============================================================================

ALTER TABLE rulesets NO FORCE ROW LEVEL SECURITY;

UPDATE rulesets
   SET config = jsonb_set(
         config,
         '{metrics}',
         ((config -> 'metrics') - 'block_point' - 'block_touch') || '["block"]'::jsonb
       )
 WHERE sport_code = 'volleyball'
   AND jsonb_typeof(config -> 'metrics') = 'array'
   AND config -> 'metrics' ? 'block_touch';

ALTER TABLE rulesets FORCE ROW LEVEL SECURITY;

UPDATE sport_metrics
   SET affects_score = false, counts_for_opponent = false, is_rankable = true, display_order = 4
 WHERE sport_code = 'volleyball' AND code = 'error';

DELETE FROM sport_metrics
 WHERE sport_code = 'volleyball' AND code = 'block_point';

UPDATE sport_metrics
   SET code = 'block', label = 'Bloqueo', display_order = 3
 WHERE sport_code = 'volleyball' AND code = 'block_touch';

UPDATE sport_metrics
   SET affects_score = false
 WHERE sport_code = 'volleyball' AND code IN ('point', 'ace');

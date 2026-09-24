-- =============================================================================
-- RemoveAssistFromFootballAndFutsal
--
-- Football and futsal stop offering "assist" as an event. Whoever records a
-- match at the side of the pitch is logging goals and cards; an assist is a
-- judgement call about the goal before it, and the catalog should not offer
-- what nobody records. Basketball keeps its own assist: there it is a
-- counted statistic.
--
-- Three things have to move together, and in this order:
--
--   1. Refuse if any event already points at the metric. player_events
--      references sport_metrics without a cascade, so the delete below would
--      fail anyway -- this says why, and leaves the decision about recorded
--      data to a person instead of deleting it here.
--
--   2. Take "assist" out of every ruleset that lists it. A ruleset naming a
--      metric its sport no longer has is refused the next time anybody edits
--      it (RulesetPolicy: "no tiene esos eventos"), over a metric they never
--      asked for.
--
--   3. Delete the metric and close the gap in display_order.
--
-- rulesets carries FORCE ROW LEVEL SECURITY, and the role that applies
-- migrations is not exempt from it: with no organization set, its UPDATE
-- would match no rows at all and report success. Forcing is lifted for the
-- one statement and put straight back; the whole migration is one
-- transaction, so nothing can observe the table unprotected.
-- =============================================================================

DO $do$
DECLARE recorded bigint;
BEGIN
    -- player_events is forced too, so this count would read zero for the
    -- same reason. Lifted for the count only.
    ALTER TABLE player_events NO FORCE ROW LEVEL SECURITY;

    SELECT count(*) INTO recorded
      FROM player_events event
      JOIN sport_metrics metric ON metric.id = event.metric_id
     WHERE metric.code = 'assist'
       AND metric.sport_code IN ('football', 'futsal');

    ALTER TABLE player_events FORCE ROW LEVEL SECURITY;

    IF recorded > 0 THEN
        RAISE EXCEPTION
            'No se puede quitar la asistencia: hay % evento(s) de asistencia registrados en fútbol o futsal. Decidí qué hacer con ellos antes de aplicar esta migración.',
            recorded;
    END IF;
END $do$;

ALTER TABLE rulesets NO FORCE ROW LEVEL SECURITY;

UPDATE rulesets
   SET config = jsonb_set(config, '{metrics}', (config -> 'metrics') - 'assist')
 WHERE sport_code IN ('football', 'futsal')
   AND jsonb_typeof(config -> 'metrics') = 'array'
   AND config -> 'metrics' ? 'assist';

ALTER TABLE rulesets FORCE ROW LEVEL SECURITY;

DELETE FROM sport_metrics
 WHERE code = 'assist'
   AND sport_code IN ('football', 'futsal');

-- goal 1, own_goal 2, yellow_card 3, red_card 4.
UPDATE sport_metrics SET display_order = 3
 WHERE sport_code IN ('football', 'futsal') AND code = 'yellow_card';

UPDATE sport_metrics SET display_order = 4
 WHERE sport_code IN ('football', 'futsal') AND code = 'red_card';

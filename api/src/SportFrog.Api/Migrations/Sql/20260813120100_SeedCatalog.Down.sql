-- Metrics cascade with the sport, but are deleted explicitly so the
-- rollback doesn't depend on the key's definition.
DELETE FROM sport_metrics
WHERE sport_code IN ('football','futsal','basketball','volleyball','wally');

DELETE FROM sports
WHERE code IN ('football','futsal','basketball','volleyball','wally');

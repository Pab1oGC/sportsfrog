-- =============================================================================
-- AddSportPeriodHasClock
--
-- "Played in sets" and "runs on a clock" turned out not to be the same fact,
-- and AddSportDefaultPeriodMinutes leaned on them being the same one: it left
-- every score_mode = 'sets' sport clockless, on the (volleyball-shaped)
-- assumption that a period decided by score never also runs on a clock.
-- Taekwondo Kyorugi disproves it -- an asalto is won by score, same as a
-- volleyball set, but it still runs two minutes on a clock same as a football
-- half. Whether a period has a clock at all is its own fact about a sport,
-- independent of how the result of one is decided.
-- =============================================================================

ALTER TABLE sports ADD COLUMN period_has_clock boolean NOT NULL DEFAULT true;

COMMENT ON COLUMN sports.period_has_clock IS
    'Whether one period of this sport runs on a clock. False for a sport whose period ends on a score instead -- default_minutes is null exactly where this is false.';

UPDATE sports SET period_has_clock = false WHERE code IN ('volleyball', 'wally', 'taekwondo_poomsae');

-- Kyorugi's asaltos run two minutes each and end early only if the point gap
-- or a golden-point score decides them first -- a clock, not a scoreline, is
-- what a reglamento form should suggest here.
UPDATE sports SET default_minutes = 2 WHERE code = 'taekwondo_kyorugi';

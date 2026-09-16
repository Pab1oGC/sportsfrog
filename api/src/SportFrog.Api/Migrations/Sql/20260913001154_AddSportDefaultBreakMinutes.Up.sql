-- =============================================================================
-- AddSportDefaultBreakMinutes
--
-- The last piece of "how long does a match actually take": period count and
-- clock length already prefill a reglamento form (see
-- AddSportDefaultPeriodMinutes) -- the rest between periods was the one
-- number still left for an organizer to type from memory. Null exactly
-- where default_minutes is null: a period with no clock has no rest between
-- periods to measure either.
-- =============================================================================

ALTER TABLE sports ADD COLUMN default_break_minutes smallint;

UPDATE sports SET default_break_minutes = 15 WHERE code = 'football';
UPDATE sports SET default_break_minutes = 10 WHERE code = 'futsal';
UPDATE sports SET default_break_minutes = 2  WHERE code = 'basketball';

-- One minute between asaltos, per the WT competition rules.
UPDATE sports SET default_break_minutes = 1  WHERE code = 'taekwondo_kyorugi';

-- volleyball, wally (score_mode = 'sets') and taekwondo_poomsae
-- (score_mode = 'judged') stay null: none of them run a clock between
-- periods, same as they carry no default_minutes.

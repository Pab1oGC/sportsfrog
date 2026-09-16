-- =============================================================================
-- AddSportDefaultPeriodMinutes
--
-- The organizer writing a reglamento already gets a default period count and
-- a default period label from the catalog (RulesetsPage prefills both the
-- moment a sport is picked) -- the clock length was the one thing still left
-- for them to type from memory. Null for a sport played in sets: there is no
-- clock to prefill, the period ends on a score (see RulesetConfiguration.
-- PeriodRules.Minutes' own remark, which already documents that rule for the
-- ruleset side of this).
-- =============================================================================

ALTER TABLE sports ADD COLUMN default_minutes smallint;

UPDATE sports SET default_minutes = 45 WHERE code = 'football';
UPDATE sports SET default_minutes = 20 WHERE code = 'futsal';
UPDATE sports SET default_minutes = 10 WHERE code = 'basketball';
-- volleyball, wally, taekwondo_kyorugi (score_mode = 'sets') and
-- taekwondo_poomsae (score_mode = 'judged') stay null: none of them play to
-- a clock.

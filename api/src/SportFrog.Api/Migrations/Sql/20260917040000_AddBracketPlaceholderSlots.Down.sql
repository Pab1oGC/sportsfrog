-- AddBracketPlaceholderSlots — rollback
--
-- Restoring NOT NULL only succeeds if nothing has used the feature yet —
-- same caveat every nullability rollback in this schema carries.

DROP INDEX IF EXISTS idx_matches_home_source;
DROP INDEX IF EXISTS idx_matches_away_source;

ALTER TABLE matches
    DROP CONSTRAINT IF EXISTS ck_home_slot_defined,
    DROP CONSTRAINT IF EXISTS ck_away_slot_defined;

ALTER TABLE matches
    DROP COLUMN IF EXISTS home_source_match_id,
    DROP COLUMN IF EXISTS away_source_match_id;

ALTER TABLE matches
    ALTER COLUMN home_team_id SET NOT NULL,
    ALTER COLUMN away_team_id SET NOT NULL;

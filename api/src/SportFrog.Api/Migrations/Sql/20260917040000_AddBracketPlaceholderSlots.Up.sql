-- =============================================================================
-- AddBracketPlaceholderSlots
--
-- Lets a knockout be drawn -- and scheduled -- all the way to the final
-- before a single match is played. Until now home_team_id/away_team_id were
-- NOT NULL, which is exactly why only round one could ever be drawn: a
-- semi-final cannot name two teams nobody has decided yet.
--
-- home_source_match_id / away_source_match_id name the earlier match whose
-- winner fills that side instead. A row now needs either a real team or a
-- source match on each side -- never neither, which is what
-- ck_home_slot_defined/ck_away_slot_defined enforce -- and once the source
-- match is played, the winner is copied into home_team_id/away_team_id
-- alongside it, not in its place: the source id stays, since it is also how
-- a bracket draws the lines connecting one round to the next.
--
-- ck_match_distinct_teams, ck_walkover_consistency and ck_result_completeness
-- need no change: a comparison against a column that is now sometimes NULL
-- already reads as "not violated" in Postgres, which is the right answer
-- for a fixture that has not named both sides yet.
-- =============================================================================

ALTER TABLE matches
    ALTER COLUMN home_team_id DROP NOT NULL,
    ALTER COLUMN away_team_id DROP NOT NULL;

ALTER TABLE matches
    ADD COLUMN home_source_match_id uuid REFERENCES matches(id) ON DELETE RESTRICT,
    ADD COLUMN away_source_match_id uuid REFERENCES matches(id) ON DELETE RESTRICT;

ALTER TABLE matches
    ADD CONSTRAINT ck_home_slot_defined
        CHECK (home_team_id IS NOT NULL OR home_source_match_id IS NOT NULL),
    ADD CONSTRAINT ck_away_slot_defined
        CHECK (away_team_id IS NOT NULL OR away_source_match_id IS NOT NULL);

CREATE INDEX idx_matches_home_source ON matches(home_source_match_id) WHERE home_source_match_id IS NOT NULL;
CREATE INDEX idx_matches_away_source ON matches(away_source_match_id) WHERE away_source_match_id IS NOT NULL;

COMMENT ON COLUMN matches.home_source_match_id IS
  'The match whose winner fills home_team_id, for a knockout drawn in full
   before it was known who reaches this round. Set at the draw and never
   cleared; the winner is copied into home_team_id once decided, alongside
   this rather than in place of it.';

COMMENT ON COLUMN matches.away_source_match_id IS
  'Same story as home_source_match_id, for the other side.';

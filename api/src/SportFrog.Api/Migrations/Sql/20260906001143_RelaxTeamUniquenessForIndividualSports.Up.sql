-- =============================================================================
-- RelaxTeamUniquenessForIndividualSports
--
-- "One team per club per category" stops being true for an individual sport:
-- five athletes of the same delegation, each a team of one, entered in the
-- same weight category is the ordinary case there, not five clubs.
--
-- The database cannot express "unique, except when this team's sport is
-- individual" directly against the existing index. A partial index's
-- predicate can only test the indexed table's own columns, and a team's
-- sport sits two joins away (teams -> categories -> competitions -> sports).
-- is_individual is copied onto the team itself, once, at the moment it is
-- entered (CreateTeam, EnrollIndividual) — a team's sport never changes
-- after that, so the copy never goes stale — purely so the index has
-- something of its own to test.
-- =============================================================================

ALTER TABLE teams
    ADD COLUMN is_individual boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN teams.is_individual IS
  'A snapshot of sports.is_individual, taken when the team was entered. Exists
   only so uq_teams_club_category can stay a plain partial index instead of a
   constraint the database has no way to express.';

DROP INDEX uq_teams_club_category;

CREATE UNIQUE INDEX uq_teams_club_category
    ON teams(category_id, club_id)
    WHERE deleted_at IS NULL AND NOT is_individual;

-- =============================================================================
-- AddTeamSeed
--
-- A group draw is not always a flat shuffle. A tournament that seeds its
-- draw — the strongest sides kept one to a group, the way an actual
-- Champions League pot works — needs somewhere to say which pot a team sits
-- in before the draw runs. This is that: an optional pot number, set by
-- hand ahead of the draw, and read by it.
--
-- No pot set on anyone is still the ordinary case: a draw where every team
-- shares one implicit pot is exactly a plain random shuffle, so this column
-- being entirely null costs the plain case nothing.
-- =============================================================================

ALTER TABLE teams ADD COLUMN seed smallint;

COMMENT ON COLUMN teams.seed IS
  'Which pot this team was placed in for a seeded group draw. Null means no
   pot — the team is drawn into a group at random rather than one-per-pot.
   Not touched by anything but the draw and whoever sets it beforehand.';

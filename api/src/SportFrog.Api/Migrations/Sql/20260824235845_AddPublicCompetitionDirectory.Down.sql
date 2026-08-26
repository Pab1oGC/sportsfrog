-- =============================================================================
-- AddPublicCompetitionDirectory - rollback
--
-- Quita el directorio, su índice y las tres ramas permisivas. Las competencias
-- publicadas siguen siendo alcanzables por su dirección, que es como se
-- llegaba a ellas antes: lo que se pierde es poder encontrarlas sin conocerla.
-- =============================================================================

DROP FUNCTION IF EXISTS list_public_competitions(text, text, text, int, int);

DROP POLICY IF EXISTS published_teams ON teams;
DROP POLICY IF EXISTS published_categories ON categories;
DROP POLICY IF EXISTS published_competitions ON competitions;

DROP INDEX IF EXISTS idx_competitions_public;

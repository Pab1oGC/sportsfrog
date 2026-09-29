-- =============================================================================
-- AddCompetitionNameUniqueness - rollback
--
-- Only the index is dropped. The names the Up migration disambiguated are not
-- restored: what they were before is not recorded anywhere, and a repeated
-- name was never a state worth going back to.
-- =============================================================================

DROP INDEX IF EXISTS uq_competitions_name;

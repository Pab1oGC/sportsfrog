-- =============================================================================
-- RelaxTeamUniquenessForIndividualSports — rollback
-- =============================================================================

DROP INDEX uq_teams_club_category;

CREATE UNIQUE INDEX uq_teams_club_category ON teams(category_id, club_id) WHERE deleted_at IS NULL;

ALTER TABLE teams DROP COLUMN is_individual;

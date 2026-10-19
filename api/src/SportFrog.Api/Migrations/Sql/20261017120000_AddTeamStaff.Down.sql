-- =============================================================================
-- AddTeamStaff - rollback
--
-- Drops the table and its enum whole. Nothing outside this migration reads
-- or writes either: a team's staff is new, and the credential pipeline reads
-- it only where it exists.
-- =============================================================================

DROP TRIGGER IF EXISTS trg_team_staff_touch ON team_staff;

DROP TABLE IF EXISTS team_staff;

DROP TYPE IF EXISTS staff_role;

-- =============================================================================
-- AddCourseSessions - rollback
--
-- Drops the table and its enum whole. Nothing outside this migration yet
-- reads or writes either one.
-- =============================================================================

DROP TRIGGER IF EXISTS trg_course_sessions_touch ON course_sessions;

DROP TABLE IF EXISTS course_sessions;

DROP TYPE IF EXISTS course_session_state;

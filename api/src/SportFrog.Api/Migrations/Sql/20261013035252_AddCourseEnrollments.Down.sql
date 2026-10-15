-- =============================================================================
-- AddCourseEnrollments - rollback
--
-- Drops the table whole. Nothing outside this migration yet reads or
-- writes it, so there is no data elsewhere that depends on it surviving a
-- rollback.
-- =============================================================================

DROP TRIGGER IF EXISTS trg_course_enrollments_touch ON course_enrollments;

DROP TABLE IF EXISTS course_enrollments;

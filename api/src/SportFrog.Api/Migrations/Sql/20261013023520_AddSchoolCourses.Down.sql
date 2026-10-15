-- =============================================================================
-- AddSchoolCourses - rollback
--
-- Drops both tables whole. Nothing outside this migration yet reads or
-- writes either one, so there is no data elsewhere that depends on them
-- surviving a rollback. course_schedules first: it references courses, and
-- dropping in dependency order reads honestly even though CASCADE would
-- not care.
-- =============================================================================

DROP TABLE IF EXISTS course_schedules;

DROP TRIGGER IF EXISTS trg_courses_touch ON courses;

DROP TABLE IF EXISTS courses;

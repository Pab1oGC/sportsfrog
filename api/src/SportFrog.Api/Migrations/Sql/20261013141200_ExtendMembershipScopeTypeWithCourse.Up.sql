-- =============================================================================
-- ExtendMembershipScopeTypeWithCourse
--
-- Adds 'course' to membership_scope_type, so a membership can be narrowed
-- to one course the same way it already narrows to one competition or one
-- club — a coach's own role, reaching only the course they teach, not the
-- whole school.
--
-- Deliberately alone in this migration, and deliberately doing nothing
-- else. PostgreSQL allows ALTER TYPE ... ADD VALUE inside a transaction
-- block, but refuses to let the new value be USED — compared, cast, stored —
-- within that same transaction; it only becomes a normal label once this
-- migration's transaction commits. Every other change the course scope
-- needs (the course_id column, the widened ck_membership_scope_target,
-- the new partial unique index) reads 'course' back as a value and so has
-- to live in a migration that runs afterward, in its own, later
-- transaction — see AddCourseScopeColumn.
-- =============================================================================

ALTER TYPE membership_scope_type ADD VALUE 'course';

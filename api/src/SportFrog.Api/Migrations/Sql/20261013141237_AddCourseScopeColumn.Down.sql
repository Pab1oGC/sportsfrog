-- =============================================================================
-- AddCourseScopeColumn - rollback
--
-- Undoes exactly this migration's own three changes, in reverse order.
-- Leaves 'course' sitting unused in membership_scope_type — nothing can
-- write a row naming it once ck_membership_scope_target is back to its
-- original two branches, so the orphaned label is inert, same reasoning as
-- ExtendMembershipScopeTypeWithCourse's own rollback.
-- =============================================================================

DROP INDEX IF EXISTS uq_membership_scope_course;

ALTER TABLE membership_scopes
    DROP CONSTRAINT ck_membership_scope_target;

ALTER TABLE membership_scopes
    ADD CONSTRAINT ck_membership_scope_target CHECK (
        (scope_type = 'competition') = (competition_id IS NOT NULL)
        AND (scope_type = 'club') = (club_id IS NOT NULL)
    );

ALTER TABLE membership_scopes
    DROP COLUMN course_id;

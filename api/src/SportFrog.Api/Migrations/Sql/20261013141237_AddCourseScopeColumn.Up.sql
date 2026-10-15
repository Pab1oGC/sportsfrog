-- =============================================================================
-- AddCourseScopeColumn
--
-- The rest of the course scope, now that 'course' is a settled label of
-- membership_scope_type (ExtendMembershipScopeTypeWithCourse, committed
-- before this migration's own transaction opened — see that migration's
-- remarks for why the two could not be one).
--
-- course_id follows competition_id's own lead, not club_id's: CASCADE,
-- because like a competition a course is only ever withdrawn by logical
-- delete (Course.DeletedAt), so this reference is never actually exercised
-- in practice — it stays CASCADE only for consistency with its sibling
-- built the same way.
--
-- ck_membership_scope_target grows its third branch the same shape as its
-- first two: scope_type names exactly which one column must be set, never
-- inferred from which happens to be non-null.
-- =============================================================================

ALTER TABLE membership_scopes
    ADD COLUMN course_id uuid REFERENCES courses(id) ON DELETE CASCADE;

ALTER TABLE membership_scopes
    DROP CONSTRAINT ck_membership_scope_target;

ALTER TABLE membership_scopes
    ADD CONSTRAINT ck_membership_scope_target CHECK (
        (scope_type = 'competition') = (competition_id IS NOT NULL)
        AND (scope_type = 'club') = (club_id IS NOT NULL)
        AND (scope_type = 'course') = (course_id IS NOT NULL)
    );

CREATE UNIQUE INDEX uq_membership_scope_course
    ON membership_scopes(membership_id, course_id)
    WHERE scope_type = 'course';

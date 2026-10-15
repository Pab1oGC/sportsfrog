-- =============================================================================
-- AddCourseEnrollments
--
-- A student registered to take a course — course_entries' counterpart to
-- roster_entries. The shape is deliberately the same: identity is by
-- athlete_id, not by name, so events recorded later (attendance, charges)
-- attach to a registration and not to a person directly, and a person
-- taking the same course twice across two enrollment windows produces two
-- enrollments, not one row quietly reused.
--
-- The three ways of leaving are the same three roster_entries already
-- keeps apart, and for the same reason:
--
--   withdrawn_at   -- left the course. Everything recorded while enrolled
--                     stays on record (attendance, charges to come).
--   deleted_at     -- the enrollment should never have been made. Refused
--                     once anything is recorded against it — that question
--                     has no answer yet (attendance/charges do not exist
--                     before later migrations), so nothing guards it today;
--                     see StrikeEnrollment's own remark.
--   athletes.is_active -- the person left the organization, which says
--                     nothing about one particular course.
--
-- uq_course_enrollments_course_athlete follows roster_entries' own
-- uq_roster_team_athlete exactly: it excludes deleted_at, not withdrawn_at.
-- A withdrawn enrollment still blocks a fresh one for the same course and
-- athlete on purpose, the same way RosterPolicy.InspectPlaceAsync refuses a
-- second registration over a withdrawn one — coming back is reinstating the
-- same enrollment (WithdrawEnrollment toggled back), not a second row.
--
-- fee_override/fee_override_reason are this enrollment's exception to the
-- course's own monthly_fee (free place, discount), and the check below
-- pairs them the same way ck_membership_scope_target pairs scope_type with
-- its target column: the amount decides the reason must exist, not the
-- other way around.
-- =============================================================================

CREATE TABLE course_enrollments (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id              uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    course_id           uuid NOT NULL REFERENCES courses(id)  ON DELETE RESTRICT,
    athlete_id          uuid NOT NULL REFERENCES athletes(id) ON DELETE RESTRICT,

    enrolled_at         timestamptz NOT NULL DEFAULT now(),
    withdrawn_at        timestamptz,

    fee_override        numeric(12,2),
    fee_override_reason text,

    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    deleted_at          timestamptz,

    CONSTRAINT ck_course_enrollments_fee_override_reason CHECK (
        (fee_override IS NULL) = (fee_override_reason IS NULL)),
    CONSTRAINT ck_course_enrollments_fee_override_non_negative CHECK (
        fee_override IS NULL OR fee_override >= 0)
);

CREATE UNIQUE INDEX uq_course_enrollments_course_athlete
    ON course_enrollments(course_id, athlete_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_course_enrollments_athlete ON course_enrollments(athlete_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_course_enrollments_course_active ON course_enrollments(course_id)
    WHERE deleted_at IS NULL AND withdrawn_at IS NULL;

COMMENT ON COLUMN course_enrollments.fee_override IS
  'In bolivianos, same as courses.monthly_fee -- see that column''s comment
   for why there is no currency column. Null means this enrollment pays the
   course''s own price; set, it replaces it (0 for a scholarship, any other
   figure for a discount).';

ALTER TABLE course_enrollments ENABLE ROW LEVEL SECURITY;
ALTER TABLE course_enrollments FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON course_enrollments
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON course_enrollments TO sportfrog_app;

CREATE TRIGGER trg_course_enrollments_touch BEFORE UPDATE ON course_enrollments
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

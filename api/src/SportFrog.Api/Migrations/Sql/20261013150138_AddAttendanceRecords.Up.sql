-- =============================================================================
-- AddAttendanceRecords
--
-- One mark, for one enrollment, on one session. Against the enrollment and
-- not the athlete — same reasoning course_enrollments itself documents for
-- hanging off an enrollment rather than a person: the same athlete taking
-- the same course across two separate enrollment windows has two
-- histories, not one shared one.
--
-- The four states (attendance_state) are SportFrog.Domain.School
-- .AttendanceState, not a new enum invented here — that type already
-- exists, in the domain project, for AttendanceRate to reduce a student's
-- marks to one rate. This is just where its values get a column; nothing
-- about counting them belongs in this migration.
--
-- No status column on course_sessions needs touching by hand to say a
-- class was dictated: the first PUT .../sessions/{id}/attendance that
-- succeeds is what moves it from scheduled to held, the same way a match
-- is never marked finished by a button of its own, only by a result being
-- recorded.
-- =============================================================================

CREATE TYPE attendance_state AS ENUM ('present', 'absent', 'late', 'excused');

CREATE TABLE attendance_records (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id        uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    session_id    uuid NOT NULL REFERENCES course_sessions(id)    ON DELETE CASCADE,
    enrollment_id uuid NOT NULL REFERENCES course_enrollments(id) ON DELETE RESTRICT,

    status        attendance_state NOT NULL,
    note          text,

    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),

    -- One mark per person per session: taking the sheet again for the same
    -- class updates it, it never adds a second one.
    UNIQUE (session_id, enrollment_id)
);

CREATE INDEX idx_attendance_records_enrollment ON attendance_records(enrollment_id);

ALTER TABLE attendance_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE attendance_records FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON attendance_records
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON attendance_records TO sportfrog_app;

CREATE TRIGGER trg_attendance_records_touch BEFORE UPDATE ON attendance_records
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- =============================================================================
-- AddCourseSessions
--
-- Turns a course's standing weekly schedule (course_schedules) into actual
-- dates to hang attendance off of — the materialization SessionPlanner,
-- in the domain project, already works out; this is where its answer gets
-- a row. See SessionPlanner's own remarks for why a recurring rule and the
-- date it lands on are different questions.
--
-- Three states, same shape as match_state's own three real ones
-- (scheduled/finished/cancelled, repechage and walkover aside): a session
-- not yet run, one that was, and one struck from the calendar. There is no
-- endpoint here that moves a session from scheduled to held — that
-- transition belongs to whoever first records something against it
-- (attendance, a later migration), the same way a match is never marked
-- "finished" by a button of its own, only by a result being recorded.
--
-- No deleted_at. A session is either generated once and left alone,
-- cancelled (status, with a reason), or moved (session_date/starts_at/
-- ends_at, by RescheduleSession) — never struck as an administrative
-- mistake the way a roster entry or an enrollment can be, because nothing
-- about materializing a date from a standing schedule is a mistake worth
-- a second bookkeeping state for. If a schedule itself was wrong, it is
-- corrected and regenerated; GenerateSessions only ever fills in dates
-- that do not already have a row (uq_course_sessions_course_date_time), so
-- fixing the schedule and running it again never duplicates anything.
--
-- ck_course_sessions_cancellation_reason pairs the two the same way
-- ck_course_enrollments_fee_override_reason already pairs an amount with
-- why it exists.
-- =============================================================================

CREATE TYPE course_session_state AS ENUM ('scheduled', 'held', 'cancelled');

CREATE TABLE course_sessions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id              uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    course_id           uuid NOT NULL REFERENCES courses(id) ON DELETE CASCADE,

    session_date        date NOT NULL,

    -- Copied from course_schedules at the moment this row is generated, not
    -- read through a join — see Course.cs's own remark on monthly_fee for
    -- the same reasoning: a schedule edited afterward must not reach back
    -- and move a class that already has a date, or one already told to its
    -- students.
    starts_at           time NOT NULL,
    ends_at             time NOT NULL,

    status              course_session_state NOT NULL DEFAULT 'scheduled',
    cancellation_reason text,

    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_course_sessions_time_range CHECK (ends_at > starts_at),
    CONSTRAINT ck_course_sessions_cancellation_reason CHECK (
        (status = 'cancelled') = (cancellation_reason IS NOT NULL))
);

-- One session per course per day and start time, not per day alone: a
-- course can meet twice on the same day (a morning and an evening group —
-- see WeeklySlot's own remarks in the domain project), and SessionPlanner
-- does not reject that, so a unique index on (course_id, session_date)
-- alone would refuse the second group's own session the moment both land
-- on the same date. What GenerateSessions' idempotency rests on, and what
-- also catches a reschedule aimed at a date and time the course already
-- meets at.
CREATE UNIQUE INDEX uq_course_sessions_course_date_time
    ON course_sessions(course_id, session_date, starts_at);

ALTER TABLE course_sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE course_sessions FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON course_sessions
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON course_sessions TO sportfrog_app;

CREATE TRIGGER trg_course_sessions_touch BEFORE UPDATE ON course_sessions
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

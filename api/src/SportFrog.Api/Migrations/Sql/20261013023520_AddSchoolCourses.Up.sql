-- =============================================================================
-- AddSchoolCourses
--
-- The first table of the school module: a course the organization runs
-- (not a competition — no ruleset, no fixture, no sorteo) and the weekly
-- slots it meets in.
--
-- A course is kept apart from a Category on purpose. A category hangs off a
-- competition and carries a ruleset because what happens in it has to be
-- scored and ranked; a course has neither, because nothing about teaching a
-- group of children on Tuesdays and Thursdays is a contest. Forcing it
-- through Category would mean inventing a competition and a ruleset that
-- answer no real question, just to get a name, a sport and a roster.
--
-- Deletion is logical, like Club's: by the time a course has students,
-- sessions and pension charges hanging off it (later migrations), erasing
-- the row would erase their history with it. There is no usage guard here
-- yet — nothing can reference a course until those migrations exist — so
-- DeleteCourse is unconditional, the same shape as DeleteClub today.
--
-- course_schedules carries no updated_at or deleted_at: a schedule change
-- replaces every row of a course at once rather than editing one in place
-- (the API's own CourseContract takes the whole set on every write), the
-- same shape accreditation_category_items already uses for a set that is
-- always replaced wholesale, never edited row by row.
--
-- monthly_fee is bolivianos only — see the column comment below for why
-- there is no currency column to say so.
-- =============================================================================

CREATE TABLE courses (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id              uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    sport_code          text NOT NULL REFERENCES sports(code),
    name                text NOT NULL,

    -- Both nullable: a course can be registered before a pitch is settled
    -- or a coach assigned, and SET NULL on either losing its target rather
    -- than blocking it — the same trade-off matches already make against
    -- venue_space_id, and no membership-removal endpoint exists yet to
    -- exercise the other side at all.
    venue_space_id      uuid REFERENCES venue_spaces(id) ON DELETE SET NULL,
    coach_membership_id uuid REFERENCES organization_memberships(id) ON DELETE SET NULL,

    capacity            smallint,
    monthly_fee         numeric(12,2) NOT NULL,
    starts_on           date NOT NULL,
    ends_on             date,
    is_active           boolean NOT NULL DEFAULT true,

    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    deleted_at          timestamptz,

    CONSTRAINT ck_courses_dates CHECK (ends_on IS NULL OR ends_on >= starts_on),
    CONSTRAINT ck_courses_capacity_positive CHECK (capacity IS NULL OR capacity > 0),
    CONSTRAINT ck_courses_fee_non_negative CHECK (monthly_fee >= 0)
);

CREATE UNIQUE INDEX uq_courses_name ON courses(org_id, name) WHERE deleted_at IS NULL;
CREATE INDEX idx_courses_org ON courses(org_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_courses_sport ON courses(org_id, sport_code) WHERE deleted_at IS NULL;
CREATE INDEX idx_courses_venue_space ON courses(venue_space_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_courses_coach_membership ON courses(coach_membership_id) WHERE deleted_at IS NULL;

COMMENT ON COLUMN courses.monthly_fee IS
  'In bolivianos. Every organization this module serves bills in no other
   currency, so there is deliberately no currency column: adding one for a
   single value nobody varies would document a question nobody is asking.
   The day that changes, it is one migration to add the column with a
   default of ''BOB'', not a rewrite of this one.';

ALTER TABLE courses ENABLE ROW LEVEL SECURITY;
ALTER TABLE courses FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON courses
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON courses TO sportfrog_app;

CREATE TRIGGER trg_courses_touch BEFORE UPDATE ON courses
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- =============================================================================
-- Weekly schedule
-- =============================================================================

CREATE TABLE course_schedules (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id      uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    course_id   uuid NOT NULL REFERENCES courses(id) ON DELETE CASCADE,

    -- .NET's DayOfWeek: 0 Sunday .. 6 Saturday. Also what JavaScript's
    -- Date#getDay() returns, so the wire value needs no translation on
    -- either side of the API.
    day_of_week smallint NOT NULL,
    starts_at   time NOT NULL,
    ends_at     time NOT NULL,

    CONSTRAINT ck_course_schedules_day CHECK (day_of_week BETWEEN 0 AND 6),
    CONSTRAINT ck_course_schedules_time_range CHECK (ends_at > starts_at)
);

COMMENT ON TABLE course_schedules IS
  'A course with two groups on the same day (a morning and an evening
   class) is two rows with the same day_of_week, and that is accepted on
   purpose — see SessionPlanner.WeeklySlot in the domain project for why
   this table does not try to rule it out.';

CREATE INDEX idx_course_schedules_course ON course_schedules(course_id);

ALTER TABLE course_schedules ENABLE ROW LEVEL SECURITY;
ALTER TABLE course_schedules FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON course_schedules
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON course_schedules TO sportfrog_app;

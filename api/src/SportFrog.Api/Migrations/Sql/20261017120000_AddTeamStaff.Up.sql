-- =============================================================================
-- AddTeamStaff
--
-- The people a team has who do not play for it: the bench and the board. They
-- are registered so they can be accredited and printed a credential, and so
-- the discipline module that follows has somebody to point at.
--
-- A table of its own rather than a flag on roster_entries. That table is what
-- makes an athlete eligible -- player_events references it, and RosterPolicy
-- holds every row of it to the category's age, weight and squad limits. A
-- forty-five-year-old coach of a Sub-15 team fails all three and would take
-- one of the squad's eighteen places. Keeping them apart is also what makes a
-- player event naming a doctor impossible rather than merely wrong: the
-- foreign key has nowhere to point.
--
-- The person is an athletes row, which is already this organization's table
-- of people -- identified by document so a later registration attaches to the
-- same person (RF-43). Nothing about a coach needs a second table of people,
-- and sharing this one is what lets athlete_accreditations and the credential
-- pipeline take them with no change at all: a credential prints no shirt
-- number and no position, so a coach fits the card exactly as a player does,
-- and the Ao "Cuerpo técnico" accreditation category that every football
-- competition is already seeded with finally has somebody to assign.
--
-- role is a closed set, unlike roster_entries.position, which is free text
-- because nothing computes from it. Something computes from this one:
-- uq_team_staff_head_coach is the database refusing a team a second director
-- técnico, and the split between cuerpo técnico and dirigentes is a
-- distinction the organizer asked for by name.
--
-- Leaving mirrors a registration's own two ways out, column for column and
-- for the same reasons -- see roster_entries in the initial schema:
-- withdrawn_at is the staff member who left mid-season and whose time here
-- stays on record, deleted_at the row that should never have been written.
-- =============================================================================

CREATE TYPE staff_role AS ENUM (
    'head_coach',
    'assistant_coach',
    'physical_trainer',
    'physician',
    'physiotherapist',
    'delegate',
    'director'
);

CREATE TABLE team_staff (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id         uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    team_id        uuid NOT NULL REFERENCES teams(id)         ON DELETE RESTRICT,
    athlete_id     uuid NOT NULL REFERENCES athletes(id)      ON DELETE RESTRICT,

    role           staff_role NOT NULL,

    registered_at  timestamptz NOT NULL DEFAULT now(),
    withdrawn_at   timestamptz,

    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    deleted_at     timestamptz
);

-- One staff row per person per team, among the living. Striking frees the
-- person to be added again; a withdrawal does not, because they genuinely
-- worked here -- the same pairing uq_roster_team_athlete already makes.
CREATE UNIQUE INDEX uq_team_staff_team_athlete ON team_staff(team_id, athlete_id)
    WHERE deleted_at IS NULL;

-- One director técnico at a time. A team changing coach mid-season withdraws
-- the first and registers the second, which is what the withdrawn_at half of
-- this filter leaves room for.
CREATE UNIQUE INDEX uq_team_staff_head_coach ON team_staff(team_id)
    WHERE role = 'head_coach' AND deleted_at IS NULL AND withdrawn_at IS NULL;

CREATE INDEX idx_team_staff_athlete ON team_staff(athlete_id) WHERE deleted_at IS NULL;

-- What the credential pipeline reads when it composes a batch: everybody
-- currently on a team's staff.
CREATE INDEX idx_team_staff_team_active ON team_staff(team_id)
    WHERE deleted_at IS NULL AND withdrawn_at IS NULL;

COMMENT ON COLUMN team_staff.athlete_id IS
  'A person from athletes, which is the organization''s table of people and
   not only of competitors -- see this migration''s own header. A coach has no
   roster entry and never gets one.';

ALTER TABLE team_staff ENABLE ROW LEVEL SECURITY;
ALTER TABLE team_staff FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON team_staff
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON team_staff TO sportfrog_app;

-- Deliberately no grant to sportfrog_public. The public portal publishes a
-- roster only where an organizer turned it on (RNF-16), and who a team's
-- doctor is was never part of that.

CREATE TRIGGER trg_team_staff_touch BEFORE UPDATE ON team_staff
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

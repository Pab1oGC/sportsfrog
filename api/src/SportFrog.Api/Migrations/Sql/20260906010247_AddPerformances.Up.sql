-- =============================================================================
-- AddPerformances
--
-- A poomsae classification stage does not pair anybody: every team competing
-- performs once, judges score it, and the field is ranked by that score
-- alone. Matches are built for two sides; this is one, so it is a table of
-- its own rather than a match with an away_team_id that is always null.
--
-- team_id, not athlete_id: a performance is scored for the competing unit —
-- one athlete, a pair or a trio, exactly as teams.is_individual already
-- represents them (RF taekwondo) — and how many roster entries stand behind
-- that team is not this table's concern.
-- =============================================================================

CREATE TYPE performance_status AS ENUM ('pending', 'scored');

CREATE TABLE performances (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,

    -- Repeated from the category rather than read through it, same as
    -- matches.competition_id: the schema puts a foreign key on it and every
    -- listing of a competition's classification stage asks for it directly.
    competition_id  uuid NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
    category_id     uuid NOT NULL REFERENCES categories(id)   ON DELETE CASCADE,
    team_id         uuid NOT NULL REFERENCES teams(id) ON DELETE RESTRICT,

    status          performance_status NOT NULL DEFAULT 'pending',

    -- Judges' score, ×100 — see PeriodScore's remarks on Home/Away for why:
    -- every score in this system is a whole number, and a judged score is
    -- the one kind with two decimal places.
    score           integer,

    recorded_by     uuid REFERENCES users(id),
    recorded_at     timestamptz,
    modified_by     uuid REFERENCES users(id),
    modified_at     timestamptz,
    notes           text,

    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz,

    CONSTRAINT ck_performance_completeness CHECK (
        (status <> 'scored') OR (score IS NOT NULL AND recorded_by IS NOT NULL)),
    CONSTRAINT ck_performance_score_nonnegative CHECK (score IS NULL OR score >= 0)
);

-- One performance slot per team per category, among the living: a team
-- withdrawn and re-entered takes the place it left, same as
-- uq_teams_club_category.
CREATE UNIQUE INDEX uq_performances_team_category ON performances(category_id, team_id)
    WHERE deleted_at IS NULL;

CREATE INDEX idx_performances_competition ON performances(competition_id, category_id)
    WHERE deleted_at IS NULL;

ALTER TABLE performances ENABLE ROW LEVEL SECURITY;
ALTER TABLE performances FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON performances
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON performances TO sportfrog_app;

CREATE TRIGGER trg_performances_touch BEFORE UPDATE ON performances
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

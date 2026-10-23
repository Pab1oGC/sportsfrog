-- =============================================================================
-- AddCompetitionAndCategoryStages
--
-- A group stage and the bracket drawn from it are not the same leg of a
-- competition, and three things that already exist have nowhere to attach
-- to one of them rather than to the whole competition or the whole
-- category: which pitches are free, how many from each group advance, who
-- actually qualified. A volleyball league that plays its groups to three
-- sets and its final to five has no way to say that at all today — the
-- only reglamento override is per category, and the group stage and the
-- final are the same category.
--
-- competition_stages holds what is the same for every category playing the
-- same leg of the same event (a pitch is booked for the competition, not
-- for one division of it); category_stages holds what varies by category —
-- the reglamento, a group's qualifiers, a bracket's entrants.
--
-- Which stages a competition actually has is never a free choice: it is
-- derived from the format it draws by and, for a group stage, the sport's
-- score mode — see SportFrog.Domain.Competitions.CompetitionStages, which
-- the backfill below mirrors in SQL so every existing competition gets
-- exactly the rows its own draw already implies.
--
-- Nothing reads these tables yet. ScheduleCalendar still reads the one
-- ScheduleSettings a competition carries as a whole; Category still carries
-- qualifiers_per_group, knockout_entrants and uses_repechage directly. This
-- migration only gives the stage a place to exist and a faithful starting
-- copy of what already applied to it, uniformly, before stages did. Moving
-- every reader onto these, one at a time, is later work — kept separate so
-- a regression in that work is never also a question about whether this
-- table has the right rows.
-- =============================================================================

CREATE TABLE competition_stages (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    competition_id  uuid NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,

    -- Free text and closed in the application, not the schema — same
    -- reasoning as competitions.format: a stage kind this migration does
    -- not yet know about is application work (teaching the draw to produce
    -- it) before it is ever a value in this column, so a CHECK constraint
    -- here would only invite the two to disagree.
    kind            text NOT NULL,

    buffer_minutes  smallint,
    space_ids       uuid[],

    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),

    -- Same bound ScheduleSettings.BufferMinutes is already validated against
    -- (see CompetitionContractValidator), stated here too so the column
    -- cannot hold what no caller was ever allowed to send it.
    CONSTRAINT ck_competition_stages_buffer_minutes
        CHECK (buffer_minutes IS NULL OR buffer_minutes BETWEEN 0 AND 120),

    -- One row per stage a competition actually has — the table this
    -- migration backfills, and the one a later caller writing to it has to
    -- respect the same way.
    UNIQUE (competition_id, kind)
);

COMMENT ON TABLE competition_stages IS
  'One leg of a competition''s draw -- a league table, a group stage, a
   bracket, a classification round -- and the scheduling logistics that
   belong to that leg rather than to the competition as a whole. Which rows
   a competition has is derived from its format and its sport''s score mode,
   never chosen freely -- see SportFrog.Domain.Competitions.CompetitionStages.';

COMMENT ON COLUMN competition_stages.kind IS
  'Which leg of the draw this is: "league", "groups", "classification" or
   "bracket". Closed in the application (CompetitionStages), not here.';

COMMENT ON COLUMN competition_stages.buffer_minutes IS
  'Minutes left on a pitch after one match of this stage before the next may
   start. Null leaves the scheduler''s own default.';

COMMENT ON COLUMN competition_stages.space_ids IS
  'Which pitches this stage may place a match on. Null or empty means none
   are enabled yet for this stage specifically.';

CREATE INDEX idx_competition_stages_competition ON competition_stages(competition_id);

ALTER TABLE competition_stages ENABLE ROW LEVEL SECURITY;
ALTER TABLE competition_stages FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON competition_stages
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON competition_stages TO sportfrog_app;

CREATE TRIGGER trg_competition_stages_touch BEFORE UPDATE ON competition_stages
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- =============================================================================

CREATE TABLE category_stages (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id                uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    category_id           uuid NOT NULL REFERENCES categories(id) ON DELETE CASCADE,

    -- Same reasoning as competition_stages.kind.
    kind                  text NOT NULL,

    -- This stage's own reglamento, overriding the category's -- the third
    -- level EffectiveRuleset does not resolve yet (it stays null on every
    -- row this migration writes). No ON DELETE clause, the same choice
    -- competitions.ruleset_id and categories.ruleset_id already make: a
    -- reglamento a stage points at cannot be removed out from under it.
    ruleset_id            uuid REFERENCES rulesets(id),

    qualifiers_per_group  smallint,
    knockout_entrants     uuid[],
    uses_repechage        boolean NOT NULL DEFAULT false,

    created_at            timestamptz NOT NULL DEFAULT now(),
    updated_at            timestamptz NOT NULL DEFAULT now(),

    -- Same bound CategoryContractValidator already enforces for
    -- QualifiersPerGroup.
    CONSTRAINT ck_category_stages_qualifiers_per_group
        CHECK (qualifiers_per_group IS NULL OR qualifiers_per_group BETWEEN 1 AND 16),

    UNIQUE (category_id, kind)
);

COMMENT ON TABLE category_stages IS
  'What varies by stage for one category: the reglamento it is played under
   where it overrides the category''s, and whatever only means something at
   that point of the draw -- a group''s qualifier count, a bracket''s
   entrants and repechage. Sibling of competition_stages, which holds what
   is the same for every category at the same leg of the same event.';

COMMENT ON COLUMN category_stages.ruleset_id IS
  'This stage''s own reglamento. Null means the category''s own answer
   applies -- see SportFrog.Api.Features.Rulebook.EffectiveRuleset, which
   does not read this column yet.';

COMMENT ON COLUMN category_stages.qualifiers_per_group IS
  'How many of this group advance, where this is a "groups" or
   "classification" stage. Meaningless, and left null, everywhere else.';

COMMENT ON COLUMN category_stages.knockout_entrants IS
  'Who qualified into this bracket, in seeded order, where this is a
   "bracket" stage drawn from an earlier one. Null before it has been drawn,
   and everywhere this is not a bracket stage.';

COMMENT ON COLUMN category_stages.uses_repechage IS
  'Whether this bracket settles third place by repechage. Only means
   something on a "bracket" row.';

CREATE INDEX idx_category_stages_category ON category_stages(category_id);
CREATE INDEX idx_category_stages_ruleset ON category_stages(ruleset_id) WHERE ruleset_id IS NOT NULL;

ALTER TABLE category_stages ENABLE ROW LEVEL SECURITY;
ALTER TABLE category_stages FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON category_stages
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON category_stages TO sportfrog_app;

CREATE TRIGGER trg_category_stages_touch BEFORE UPDATE ON category_stages
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- =============================================================================
-- Backfill: one row per stage every existing competition's own draw already
-- implies, and the matching per-category row carrying whatever of its
-- qualifiers/entrants/repechage/schedule belongs to that stage.
--
-- Walked per organization, not as one global INSERT ... SELECT: the role
-- this migration runs as has no BYPASSRLS (see
-- 20261005163038_SeedFootballAccreditationCatalogPerOrganization for the
-- same lesson learned the hard way), so current_org_id() has to be set
-- before a write this policy will accept lands.
-- =============================================================================

DO $$
DECLARE
    org RECORD;
BEGIN
    FOR org IN SELECT id FROM organizations ORDER BY id LOOP
        PERFORM set_config('app.current_org', org.id::text, true);

        INSERT INTO competition_stages (org_id, competition_id, kind, buffer_minutes, space_ids)
        SELECT
            c.org_id,
            c.id,
            stage.kind,
            (c.settings -> 'schedule' ->> 'buffer_minutes')::smallint,
            CASE WHEN c.settings -> 'schedule' -> 'space_ids' IS NOT NULL
                 THEN (SELECT array_agg(value::uuid)
                       FROM jsonb_array_elements_text(c.settings -> 'schedule' -> 'space_ids'))
                 ELSE NULL
            END
        FROM competitions c
        JOIN sports sp ON sp.code = c.sport_code
        CROSS JOIN LATERAL (
            SELECT unnest(
                CASE
                    WHEN c.format = 'league' THEN ARRAY['league']
                    WHEN c.format = 'knockout' THEN ARRAY['bracket']
                    WHEN c.format = 'groups' AND sp.score_mode = 'judged'
                        THEN ARRAY['classification', 'bracket']
                    WHEN c.format = 'groups' THEN ARRAY['groups', 'bracket']
                    ELSE ARRAY[]::text[]
                END
            ) AS kind
        ) AS stage
        WHERE c.org_id = org.id;

        INSERT INTO category_stages (
            org_id, category_id, kind, qualifiers_per_group, knockout_entrants, uses_repechage
        )
        SELECT
            cat.org_id,
            cat.id,
            stage.kind,
            CASE WHEN stage.kind IN ('groups', 'classification') THEN cat.qualifiers_per_group END,
            CASE WHEN stage.kind = 'bracket' THEN cat.knockout_entrants END,
            CASE WHEN stage.kind = 'bracket' THEN cat.uses_repechage ELSE false END
        FROM categories cat
        JOIN competitions c ON c.id = cat.competition_id
        JOIN sports sp ON sp.code = c.sport_code
        CROSS JOIN LATERAL (
            SELECT unnest(
                CASE
                    WHEN c.format = 'league' THEN ARRAY['league']
                    WHEN c.format = 'knockout' THEN ARRAY['bracket']
                    WHEN c.format = 'groups' AND sp.score_mode = 'judged'
                        THEN ARRAY['classification', 'bracket']
                    WHEN c.format = 'groups' THEN ARRAY['groups', 'bracket']
                    ELSE ARRAY[]::text[]
                END
            ) AS kind
        ) AS stage
        WHERE cat.org_id = org.id;
    END LOOP;

    PERFORM set_config('app.current_org', '', true);
END $$;

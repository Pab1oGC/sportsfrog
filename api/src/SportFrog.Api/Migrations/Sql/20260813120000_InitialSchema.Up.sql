-- =============================================================================
-- SPORTFROG — Initial schema
-- PostgreSQL 16+
--
-- Conventions:
--   · Identifiers in snake_case, tables in plural.
--   · Every business data table includes org_id, a requirement for resolving
--     isolation policies without traversing intermediate tables.
--   · Timestamps in timestamptz, always in UTC.
--   · Change authorship lives in audit_log and isn't duplicated as per-table
--     columns.
--
-- Runs as the schema owner user, not the application user: it creates
-- extensions, roles and SECURITY DEFINER functions.
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS citext;

-- =============================================================================
-- 1. ENUM TYPES
-- =============================================================================

CREATE TYPE membership_role   AS ENUM ('owner','admin','operator','recorder','viewer');
CREATE TYPE competition_state AS ENUM ('draft','scheduled','in_progress','finished','cancelled');
CREATE TYPE match_state       AS ENUM ('scheduled','in_progress','finished','postponed','walkover','cancelled');
CREATE TYPE capture_level     AS ENUM ('basic','detailed');
CREATE TYPE document_kind     AS ENUM ('credential','certificate');
CREATE TYPE document_state    AS ENUM ('issued','revoked');

-- =============================================================================
-- 2. PLATFORM
-- =============================================================================

CREATE TABLE organizations (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            text        NOT NULL,
    slug            citext      NOT NULL UNIQUE,
    logo_url        text,
    plan            text        NOT NULL DEFAULT 'free',
    is_active       boolean     NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz
);

COMMENT ON COLUMN organizations.plan IS
  'Reserved for future billing. No functional effect in v1.';
COMMENT ON COLUMN organizations.slug IS
  'First segment of the public URL. Resolves the organization before
   establishing the isolation context on anonymous access.';

CREATE TABLE users (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email           citext      NOT NULL UNIQUE,
    password_hash   text        NOT NULL,
    full_name       text        NOT NULL,
    is_active       boolean     NOT NULL DEFAULT true,
    last_login_at   timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz
);

COMMENT ON COLUMN users.deleted_at IS
  'Soft delete is mandatory: the user is referenced as the author of
   results, documents and audit log entries.';

CREATE TABLE organization_memberships (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    user_id         uuid NOT NULL REFERENCES users(id)         ON DELETE CASCADE,
    role            membership_role NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (org_id, user_id)
);
CREATE INDEX idx_memberships_user ON organization_memberships(user_id);

CREATE TABLE refresh_tokens (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash      text        NOT NULL UNIQUE,
    expires_at      timestamptz NOT NULL,
    revoked_at      timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_refresh_user_active ON refresh_tokens(user_id) WHERE revoked_at IS NULL;

-- =============================================================================
-- 3. SPORTS CATALOG
-- Content shared by every organization. Managed through migrations.
-- =============================================================================

CREATE TABLE sports (
    code            text PRIMARY KEY,
    name            text     NOT NULL,
    period_label    text     NOT NULL,
    default_periods smallint NOT NULL,
    scoring_unit    text     NOT NULL,
    score_mode      text     NOT NULL DEFAULT 'cumulative'
                             CHECK (score_mode IN ('cumulative','sets')),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE sport_metrics (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    sport_code      text NOT NULL REFERENCES sports(code) ON DELETE CASCADE,
    code            text     NOT NULL,
    label           text     NOT NULL,
    affects_score   boolean  NOT NULL DEFAULT false,
    is_rankable     boolean  NOT NULL DEFAULT true,
    display_order   smallint NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (sport_code, code)
);

-- =============================================================================
-- 4. RULESETS
-- =============================================================================

CREATE TABLE rulesets (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    sport_code      text NOT NULL REFERENCES sports(code),
    name            text        NOT NULL,
    config          jsonb       NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_rulesets_config CHECK (
        config ? 'periods' AND config ? 'points' AND config ? 'tiebreakers'
    )
);
CREATE INDEX idx_rulesets_org ON rulesets(org_id);

COMMENT ON COLUMN rulesets.config IS $comment$Expected structure:
{
  "periods":     { "count": 2, "label": "half", "minutes": 45 },
  "points":      { "win": 3, "draw": 1, "loss": 0 },
  "tiebreakers": ["score_difference","score_for","head_to_head"],
  "walkover":    { "winner_score": 3, "loser_score": 0 },
  "metrics":     ["goal","yellow_card","red_card","assist"]
}
For sports with score_mode = 'sets', points also accepts the form
{ "win_3_0": 3, "win_2_1": 2, "loss_1_2": 1, "loss_0_3": 0 }.
The order of the tiebreakers array is significant: applied in sequence.
The full structure is validated against a schema in the application layer;
the table constraint only checks that the required keys are present.$comment$;

-- =============================================================================
-- 5. CLUBS AND ATHLETES
-- =============================================================================

CREATE TABLE clubs (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    name            text        NOT NULL,
    short_name      text,
    logo_url        text,
    is_active       boolean     NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz
);
CREATE UNIQUE INDEX uq_clubs_name ON clubs(org_id, name) WHERE deleted_at IS NULL;
CREATE INDEX idx_clubs_org ON clubs(org_id) WHERE deleted_at IS NULL;

CREATE TABLE athletes (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    first_name      text        NOT NULL,
    last_name       text        NOT NULL,
    document_id     text        NOT NULL,
    birth_date      date        NOT NULL,
    gender          text,
    photo_url       text,
    guardian_name   text,
    guardian_phone  text,
    is_active       boolean     NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz
);
-- Document uniqueness within the organization is what supports reusing the
-- athlete across competitions, as required by RF-43.
CREATE UNIQUE INDEX uq_athletes_document ON athletes(org_id, document_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_athletes_org ON athletes(org_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_athletes_name ON athletes(org_id, last_name, first_name) WHERE deleted_at IS NULL;

COMMENT ON COLUMN athletes.photo_url IS
  'Image already normalized in orientation, dimensions and format. The
   original uploaded image is not kept.';

-- =============================================================================
-- 6. COMPETITIONS
-- =============================================================================

CREATE TABLE competitions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    sport_code      text NOT NULL REFERENCES sports(code),
    ruleset_id      uuid NOT NULL REFERENCES rulesets(id),
    name            text        NOT NULL,
    slug            citext      NOT NULL,
    season          text        NOT NULL,
    format          text        NOT NULL,
    capture_level   capture_level     NOT NULL DEFAULT 'basic',
    status          competition_state NOT NULL DEFAULT 'draft',
    starts_on       date,
    ends_on         date,
    is_public       boolean     NOT NULL DEFAULT false,
    settings        jsonb       NOT NULL DEFAULT '{}'::jsonb,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz,
    CONSTRAINT ck_competition_dates CHECK (
        ends_on IS NULL OR starts_on IS NULL OR ends_on >= starts_on)
);
CREATE INDEX idx_competitions_org ON competitions(org_id, status) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX uq_competitions_slug ON competitions(org_id, slug) WHERE deleted_at IS NULL;

COMMENT ON COLUMN competitions.slug IS
  'Second segment of the public URL.';

COMMENT ON COLUMN competitions.settings IS $comment$Expected structure:
{
  "schedule": {
    "slot_minutes": 90,
    "spaces": [
      { "venue_space_id": "uuid", "days": [6,0], "from": "08:00", "to": "14:00" }
    ]
  },
  "public": { "show_standings": true, "show_leaders": true, "show_rosters": false }
}$comment$;

CREATE TABLE categories (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    competition_id  uuid NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
    ruleset_id      uuid REFERENCES rulesets(id),
    name            text     NOT NULL,
    gender          text,
    birth_date_from date,
    birth_date_to   date,
    max_roster_size smallint,
    display_order   smallint NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (competition_id, name)
);
CREATE INDEX idx_categories_competition ON categories(competition_id);

CREATE TABLE venues (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    name            text    NOT NULL,
    address         text,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (org_id, name)
);

CREATE TABLE venue_spaces (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    venue_id        uuid NOT NULL REFERENCES venues(id) ON DELETE CASCADE,
    name            text    NOT NULL,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (venue_id, name)
);
CREATE INDEX idx_venue_spaces_venue ON venue_spaces(venue_id) WHERE is_active;

COMMENT ON TABLE venue_spaces IS
  'A venue with a single space is represented by a single row, so the model
   doesn''t distinguish the simple case from the composite one.';

CREATE TABLE teams (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    club_id         uuid NOT NULL REFERENCES clubs(id)      ON DELETE RESTRICT,
    category_id     uuid NOT NULL REFERENCES categories(id) ON DELETE RESTRICT,
    name            text        NOT NULL,
    group_label     text,
    is_active       boolean     NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz
);
CREATE UNIQUE INDEX uq_teams_club_category ON teams(category_id, club_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_teams_category ON teams(category_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_teams_club ON teams(club_id) WHERE deleted_at IS NULL;

COMMENT ON TABLE teams IS
  'Represents a club''s participation in a category of a competition. The
   same club produces one row per category it participates in.';

-- withdrawn_at is a sporting withdrawal (the player leaves the team mid
-- competition, keeping the events already recorded). deleted_at is an
-- administrative correction of a mistaken enrollment. Distinct facts.
CREATE TABLE roster_entries (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    team_id         uuid NOT NULL REFERENCES teams(id)    ON DELETE RESTRICT,
    athlete_id      uuid NOT NULL REFERENCES athletes(id) ON DELETE RESTRICT,
    jersey_number   smallint,
    position        text,
    registered_at   timestamptz NOT NULL DEFAULT now(),
    withdrawn_at    timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz
);
CREATE UNIQUE INDEX uq_roster_team_athlete ON roster_entries(team_id, athlete_id) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX uq_roster_jersey ON roster_entries(team_id, jersey_number)
    WHERE deleted_at IS NULL AND withdrawn_at IS NULL AND jersey_number IS NOT NULL;
CREATE INDEX idx_roster_athlete ON roster_entries(athlete_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_roster_team_active ON roster_entries(team_id)
    WHERE deleted_at IS NULL AND withdrawn_at IS NULL;

-- =============================================================================
-- 7. SCHEDULE AND RESULTS
-- =============================================================================

CREATE TABLE matches (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id           uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    competition_id   uuid NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
    category_id      uuid NOT NULL REFERENCES categories(id)   ON DELETE CASCADE,
    home_team_id     uuid NOT NULL REFERENCES teams(id) ON DELETE RESTRICT,
    away_team_id     uuid NOT NULL REFERENCES teams(id) ON DELETE RESTRICT,
    venue_space_id   uuid REFERENCES venue_spaces(id) ON DELETE SET NULL,
    round_number     smallint,
    phase            text,
    scheduled_at     timestamptz,
    status           match_state NOT NULL DEFAULT 'scheduled',
    walkover_team_id uuid REFERENCES teams(id),
    -- Score by period: periods are never read or written separately from the
    -- match, and their count varies by sport.
    period_scores    jsonb,
    home_total       integer,
    away_total       integer,
    -- Result authorship, kept because it's business data and not a technical
    -- trace: answers who recorded the result and who corrected it afterward.
    recorded_by      uuid REFERENCES users(id),
    recorded_at      timestamptz,
    modified_by      uuid REFERENCES users(id),
    modified_at      timestamptz,
    notes            text,
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now(),
    deleted_at       timestamptz,
    CONSTRAINT ck_match_distinct_teams CHECK (home_team_id <> away_team_id),
    CONSTRAINT ck_walkover_consistency CHECK (
        (status = 'walkover') = (walkover_team_id IS NOT NULL)),
    CONSTRAINT ck_result_completeness CHECK (
        (status <> 'finished') OR
        (home_total IS NOT NULL AND away_total IS NOT NULL AND recorded_by IS NOT NULL))
);
CREATE INDEX idx_matches_competition ON matches(competition_id, category_id, scheduled_at)
    WHERE deleted_at IS NULL;
CREATE INDEX idx_matches_teams ON matches(home_team_id, away_team_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_matches_status ON matches(competition_id, status) WHERE deleted_at IS NULL;

COMMENT ON COLUMN matches.period_scores IS
  'Array of scores by period: [{"p":1,"h":2,"a":1},{"p":2,"h":1,"a":0}].
   home_total and away_total consolidate the result according to
   sports.score_mode: in volleyball and wally they represent sets won, not
   points.';

CREATE UNIQUE INDEX uq_space_schedule
    ON matches(venue_space_id, scheduled_at)
    WHERE venue_space_id IS NOT NULL
      AND scheduled_at IS NOT NULL
      AND deleted_at IS NULL
      AND status NOT IN ('cancelled','postponed');

-- Events are physically deleted: they're capture corrections, not history in
-- themselves. Their trace remains in audit_log alongside the result change.
CREATE TABLE player_events (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    match_id        uuid NOT NULL REFERENCES matches(id)        ON DELETE CASCADE,
    roster_entry_id uuid NOT NULL REFERENCES roster_entries(id) ON DELETE RESTRICT,
    metric_id       uuid NOT NULL REFERENCES sport_metrics(id),
    period_number   smallint,
    minute          smallint,
    quantity        integer     NOT NULL DEFAULT 1 CHECK (quantity > 0),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_events_match ON player_events(match_id);
CREATE INDEX idx_events_roster ON player_events(roster_entry_id, metric_id);
CREATE INDEX idx_events_metric ON player_events(metric_id, roster_entry_id);

COMMENT ON TABLE player_events IS
  'The correspondence between the metric and the sport of the match''s
   competition is validated in the application layer: the constraint can''t
   be expressed as a foreign key without denormalizing the sport onto this
   table.';
-- =============================================================================
-- 9. DOCUMENTS
-- =============================================================================

CREATE TABLE document_templates (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    kind            document_kind NOT NULL,
    name            text        NOT NULL,
    layout          jsonb       NOT NULL,
    page_size       text        NOT NULL DEFAULT 'credential',
    is_default      boolean     NOT NULL DEFAULT false,
    version         integer     NOT NULL DEFAULT 1,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz
);
CREATE INDEX idx_templates_org ON document_templates(org_id, kind) WHERE deleted_at IS NULL;

COMMENT ON COLUMN document_templates.deleted_at IS
  'Soft delete is mandatory: documents already issued reference the template
   and its version so the original can be reproduced on reissue.';

COMMENT ON COLUMN document_templates.layout IS $comment$Background image plus
fields positioned over it. Supports front and back:
{
  "front": {
    "background_url": "https://.../front.png",
    "aspect_ratio": 1.586,
    "fields": [
      { "source":"athlete.photo",     "x":0.14, "y":0.15, "w":0.24, "h":0.45 },
      { "source":"athlete.full_name", "x":0.14, "y":0.72, "size":0.06,
        "font":"inter", "align":"left", "fit":"shrink", "min_size":0.04 },
      { "source":"document.qr",       "x":0.80, "y":0.60, "w":0.15, "h":0.15 }
    ]
  },
  "back": { "background_url": "...", "fields": [ ... ] }
}
Conventions:
  · x, y, w, h and size are expressed between 0 and 1, relative to the width
    and height of the background, so replacing the background with an image
    of a different resolution doesn't invalidate the positioning.
  · fit declares the behavior when content exceeds the space: 'shrink'
    reduces the size down to min_size, 'wrap' distributes across lines,
    'truncate' clips.
  · font is taken from a closed set served by the system itself, so the
    browser and the document generator use the same typeface.
Validated against a schema before persisting. Does not accept executable
content.$comment$;

-- Does not support deletion, physical or logical: cancellation is expressed
-- through status and revoked_at, which is the business fact the system must
-- keep.
CREATE TABLE issued_documents (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id            uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    template_id       uuid NOT NULL REFERENCES document_templates(id) ON DELETE RESTRICT,
    template_version  integer       NOT NULL,
    kind              document_kind NOT NULL,
    competition_id    uuid NOT NULL REFERENCES competitions(id) ON DELETE RESTRICT,
    athlete_id        uuid REFERENCES athletes(id) ON DELETE RESTRICT,
    team_id           uuid REFERENCES teams(id)    ON DELETE RESTRICT,
    serial_number     text        NOT NULL,
    certificate_type  text,
    valid_from        date,
    valid_to          date,
    pdf_url           text,
    status            document_state NOT NULL DEFAULT 'issued',
    issued_by         uuid NOT NULL REFERENCES users(id),
    issued_at         timestamptz NOT NULL DEFAULT now(),
    revoked_by        uuid REFERENCES users(id),
    revoked_at        timestamptz,
    revocation_reason text,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE (org_id, serial_number),
    CONSTRAINT ck_revocation CHECK ((status = 'revoked') = (revoked_at IS NOT NULL)),
    CONSTRAINT ck_document_subject CHECK (athlete_id IS NOT NULL OR team_id IS NOT NULL)
);
CREATE INDEX idx_issued_competition ON issued_documents(competition_id, kind, status);
CREATE INDEX idx_issued_athlete ON issued_documents(athlete_id);

COMMENT ON COLUMN issued_documents.serial_number IS
  'Unique within the organization. The code printed on the credential
   combines the organization identifier with this number, so public
   verification resolves both before establishing the isolation context.
   Requirement RF-45.';

-- =============================================================================
-- 10. AUDIT LOG
-- =============================================================================

CREATE TABLE audit_log (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    org_id          uuid        NOT NULL,
    user_id         uuid,
    user_email      text,
    entity_type     text        NOT NULL,
    entity_id       uuid,
    action          text        NOT NULL,     -- 'create','update','delete','revoke'
    changes         jsonb,
    reason          text,
    ip_address      inet,
    occurred_at     timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_audit_org_time ON audit_log(org_id, occurred_at DESC);
CREATE INDEX idx_audit_entity ON audit_log(entity_type, entity_id);

COMMENT ON TABLE audit_log IS
  'Deliberately without foreign keys: the record must be kept even if the
   referenced entity is deleted. User data is copied verbatim for the same
   reason. It is the single source of change authorship in the system;
   business tables don''t duplicate that information in their own columns.
   Records both physical deletes of configuration entities and every
   operation on results and documents.';

-- =============================================================================
-- 11. ISOLATION BETWEEN ORGANIZATIONS
-- =============================================================================

CREATE OR REPLACE FUNCTION current_org_id() RETURNS uuid AS $fn$
    SELECT NULLIF(current_setting('app.current_org', true), '')::uuid;
$fn$ LANGUAGE sql STABLE;

DO $do$
DECLARE t text;
BEGIN
    FOREACH t IN ARRAY ARRAY[
        'organization_memberships','rulesets','clubs','athletes','competitions',
        'categories','venues','venue_spaces','teams','roster_entries','matches',
        'player_events','document_templates','issued_documents','audit_log'
    ] LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
        EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', t);
        EXECUTE format($f$
            CREATE POLICY tenant_isolation ON %I
            USING (org_id = current_org_id())
            WITH CHECK (org_id = current_org_id())
        $f$, t);
    END LOOP;
END $do$;

-- The context is established per transaction with SET LOCAL and never per
-- connection. If it wasn't set, current_org_id() returns NULL and no row
-- satisfies the policy: the system fails closed.
--
-- The soft-delete filter (deleted_at IS NULL) is NOT implemented as a
-- policy: it's a business visibility rule, not a security one, and must be
-- possible to bypass for administrative recovery queries. It's applied as a
-- global filter in the object-relational mapper.

-- =============================================================================
-- 12. APPLICATION USERS
-- =============================================================================
--
-- Roles are created here only if the environment didn't provision them
-- earlier. In the dockerized environment they're created by the
-- initialization script, which is the only place passwords live. This
-- migration never sets passwords.

DO $do$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sportfrog_app') THEN
        CREATE ROLE sportfrog_app LOGIN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sportfrog_public') THEN
        CREATE ROLE sportfrog_public LOGIN;
    END IF;
END $do$;

-- Administrative access: read and write, always under an established
-- organization context.
GRANT USAGE ON SCHEMA public TO sportfrog_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sportfrog_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO sportfrog_app;

-- Public query: read-only, and only on the tables the public view needs. The
-- read-only condition is guaranteed by the engine and not by the absence of
-- write operations in the code.
GRANT USAGE ON SCHEMA public TO sportfrog_public;
GRANT SELECT ON
    organizations, competitions, categories, venues, venue_spaces, clubs, teams,
    roster_entries, athletes, matches, player_events, sports, sport_metrics,
    rulesets, issued_documents
TO sportfrog_public;

-- Resolving the public URL queries organizations and competitions before
-- establishing the isolation context. organizations carries no row-level
-- policy; competitions does, so that specific resolution is done through a
-- function with the owner's privileges, scoped to checking existence,
-- activity and public status.
CREATE OR REPLACE FUNCTION resolve_public_competition(p_org_slug citext, p_comp_slug citext)
RETURNS TABLE (org_id uuid, competition_id uuid)
LANGUAGE sql SECURITY DEFINER STABLE AS $fn$
    SELECT o.id, c.id
    FROM organizations o
    JOIN competitions c ON c.org_id = o.id
    WHERE o.slug = p_org_slug
      AND c.slug = p_comp_slug
      AND o.is_active
      AND o.deleted_at IS NULL
      AND c.is_public
      AND c.deleted_at IS NULL;
$fn$;
REVOKE ALL ON FUNCTION resolve_public_competition(citext, citext) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION resolve_public_competition(citext, citext) TO sportfrog_public;

-- =============================================================================
-- 13. TRIGGERS
-- =============================================================================

CREATE OR REPLACE FUNCTION touch_updated_at() RETURNS trigger AS $fn$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END $fn$ LANGUAGE plpgsql;

DO $do$
DECLARE t text;
BEGIN
    FOREACH t IN ARRAY ARRAY[
        'organizations','users','organization_memberships','refresh_tokens',
        'sports','sport_metrics','rulesets','clubs','athletes','competitions',
        'categories','venues','venue_spaces','teams','roster_entries','matches',
        'player_events','document_templates','issued_documents'
    ] LOOP
        EXECUTE format(
            'CREATE TRIGGER trg_%s_touch BEFORE UPDATE ON %I
             FOR EACH ROW EXECUTE FUNCTION touch_updated_at()', t, t);
    END LOOP;
END $do$;

-- Prevents modification and deletion on the audit log, even from the
-- application.
CREATE OR REPLACE FUNCTION reject_audit_mutation() RETURNS trigger AS $fn$
BEGIN
    RAISE EXCEPTION 'The audit log is append-only';
END $fn$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_immutable
    BEFORE UPDATE OR DELETE ON audit_log
    FOR EACH STATEMENT EXECUTE FUNCTION reject_audit_mutation();

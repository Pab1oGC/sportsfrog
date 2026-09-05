-- =============================================================================
-- AddTaekwondoCatalogFoundations
--
-- Schema and catalog groundwork for taekwondo's two modalities. Every new
-- column is nullable or defaulted, so no existing row changes shape and no
-- existing query changes behavior.
--
-- Kyorugi (combat) fits the sport catalog today: a match decided by winning
-- the majority of a fixed number of rounds is exactly what score_mode =
-- 'sets' already models (SportFrog.Domain.Rules.SetsMatchOutcomeRules), with
-- 'asalto' as this sport's period label and best-of-three as its default.
-- Its catalog row is seeded here.
--
-- Poomsae (judged forms) is not: it is scored against a rubric, not decided
-- by rounds won, and needs a score_mode the domain does not implement yet.
-- The CHECK constraint below is widened to admit 'judged' as a legal value
-- ahead of that, but no row uses it yet. SportConfiguration.ScoreModeConverter
-- still maps anything other than 'sets' to ScoreMode.Cumulative, so a sports
-- row stored with score_mode = 'judged' today would silently read back as
-- Cumulative and be scored wrong, not fail loudly. Poomsae's catalog row is
-- seeded only once that converter, the ScoreMode enum, and a
-- JudgedMatchOutcomeRules implementation exist together.
-- =============================================================================

-- ---- Sports catalog: is the entrant one athlete, not a squad? --------------
--
-- Every roster still runs through a team row (roster_entries -> teams ->
-- clubs), even for one person: the delegation is carried by the team's club,
-- and an individual entrant is simply a team of one. This flag is what tells
-- the application to relax "one team per club per category" for a sport
-- where an athlete, not a squad, is the entrant — the uniqueness itself
-- stays a database constraint, loosened in the migration that introduces the
-- application-level check that replaces it.
ALTER TABLE sports
    ADD COLUMN is_individual boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN sports.is_individual IS
  'True for a sport whose entrant is one athlete rather than a squad — a
   team of one, still fielded through the same teams/roster_entries tables,
   with its delegation carried by the team''s club.';

-- ---- Room for a score mode the domain does not implement yet ---------------
DO $$
DECLARE
    existing_check text;
BEGIN
    SELECT conname INTO existing_check
    FROM pg_constraint
    WHERE conrelid = 'sports'::regclass
      AND contype = 'c'
      AND pg_get_constraintdef(oid) LIKE '%score_mode%';

    EXECUTE format('ALTER TABLE sports DROP CONSTRAINT %I', existing_check);
END $$;

ALTER TABLE sports
    ADD CONSTRAINT ck_sports_score_mode CHECK (score_mode IN ('cumulative', 'sets', 'judged'));

-- ---- Weight classes: real fields, validated like every other eligibility ---
--
-- Sit alongside the sex and birth-date-window restrictions a category
-- already carries. Both null leaves the category unweighted, exactly like an
-- open sex or birth-date window today.
ALTER TABLE categories
    ADD COLUMN min_weight_kg numeric(5,2),
    ADD COLUMN max_weight_kg numeric(5,2);

ALTER TABLE categories
    ADD CONSTRAINT ck_categories_min_weight_positive
        CHECK (min_weight_kg IS NULL OR min_weight_kg > 0),
    ADD CONSTRAINT ck_categories_max_weight_positive
        CHECK (max_weight_kg IS NULL OR max_weight_kg > 0),
    ADD CONSTRAINT ck_categories_weight_range_valid
        CHECK (min_weight_kg IS NULL OR max_weight_kg IS NULL OR min_weight_kg <= max_weight_kg);

COMMENT ON COLUMN categories.min_weight_kg IS
  'Lightest athlete this category admits, in kilograms. Null leaves it open at the bottom.';
COMMENT ON COLUMN categories.max_weight_kg IS
  'Heaviest athlete this category admits, in kilograms. Null leaves it open at the top.';

ALTER TABLE athletes
    ADD COLUMN weight_kg numeric(5,2);

ALTER TABLE athletes
    ADD CONSTRAINT ck_athletes_weight_positive CHECK (weight_kg IS NULL OR weight_kg > 0);

COMMENT ON COLUMN athletes.weight_kg IS
  'Most recent weigh-in on record, in kilograms. Null until one is recorded;
   this is what a weight-classed category''s eligibility check reads.';

-- ---- The delegation for an athlete who has none ----------------------------
--
-- teams.club_id stays NOT NULL: rather than teaching every reader of
-- team.club.name (credentials, the public portal, listings) to handle a
-- missing delegation, an athlete with none enrolls under this club like any
-- other. One such club per organization; created lazily by the application
-- the first time it is needed, not seeded here — this migration only adds
-- the column that marks it and the index that keeps it singular.
ALTER TABLE clubs
    ADD COLUMN is_unaffiliated boolean NOT NULL DEFAULT false;

CREATE UNIQUE INDEX uq_clubs_unaffiliated ON clubs(org_id) WHERE is_unaffiliated AND deleted_at IS NULL;

COMMENT ON COLUMN clubs.is_unaffiliated IS
  'The one club, per organization, that an athlete with no delegation of
   their own enrolls under. Not a real club, so it is excluded from listings
   and pickers meant for clubs a delegate manages.';

-- ---- Kyorugi: fits score_mode = 'sets' today -------------------------------
INSERT INTO sports (code, name, period_label, default_periods, scoring_unit, score_mode, is_individual) VALUES
    ('taekwondo_kyorugi', 'Taekwondo (Kyorugi)', 'asalto', 3, 'punto', 'sets', true);

-- Non-scoring, like every metric of a sets-mode sport (the header comment in
-- SeedCatalog explains why): the match result is asaltos won, consolidated
-- from period_scores, not summed from these events. They exist for the
-- record and for leaderboards, not to drive the result.
INSERT INTO sport_metrics (sport_code, code, label, affects_score, is_rankable, display_order) VALUES
    ('taekwondo_kyorugi', 'point',   'Punto',    false, true, 1),
    ('taekwondo_kyorugi', 'penalty', 'Gam-jeom', false, true, 2);

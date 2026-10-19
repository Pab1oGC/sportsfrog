-- =============================================================================
-- AddSanctions
--
-- One disciplinary sanction against one person, for one competition: what
-- was done, why, and the terms that clear it.
--
-- athlete_id names a person from athletes, the organization's table of
-- people and not only of competitors -- the same reasoning team_staff
-- already follows: a coach sent off is disciplined the same way a player is,
-- with nothing here needing to know which of the two they are.
--
-- team_id is copied onto the row rather than read through a roster entry or
-- a staff registration, and deliberately so: a sanction outlives either.
-- Somebody can be withdrawn from the team long after a suspension imposed
-- while they were on it is still being served -- counting the matches that
-- clear it needs to keep naming the team regardless of whether the
-- sanctioned person is still registered to any of them.
--
-- completion_rule says how the sanction is cleared: by a number of matches,
-- by a fine, or only once both are. The CHECK constraints below are the
-- database refusing to trust that the application always pairs
-- matches_required and fine_amount with the rule that needs them -- the same
-- shape ck_payment_receipt already is for payment_method and receipt_key.
--
-- No status column, for the same reason tuition_charges carries none:
-- SportFrog.Domain.Discipline.SanctionLedger resolves Active/Served/
-- Lifted/Voided fresh every time from this row's own terms against what has
-- actually been served, so an appeal that reopens a sanction already marked
-- "cumplida" cannot leave a stale answer lying around for nobody to update.
--
-- fine_waived_at/fine_waiver_reason, lifted_at/lift_reason and
-- voided_at/void_reason are three independent toggles, not one "resolved"
-- flag -- the same shape tuition_charges.exonerated_at and voided_at already
-- are relative to each other. A sanction can be lifted on appeal and later
-- discovered to have named the wrong person entirely; SanctionLedger already
-- resolves that combination (voided outranks lifted) without this table
-- needing to forbid it.
--
-- credential_withheld_at is not a revocation. issued_documents.status stays
-- 'issued' -- the card itself is not void, it is sitting in a drawer until
-- the sanction clears or the person is disciplined some other way. Printing
-- four hundred new cards over a single suspension would be absurd; this is
-- the fact that the one card already printed is being held back.
-- =============================================================================

CREATE TYPE sanction_completion_rule AS ENUM ('matches', 'fine', 'both');

CREATE TABLE sanctions (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id                  uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    competition_id          uuid NOT NULL REFERENCES competitions(id)  ON DELETE RESTRICT,
    athlete_id              uuid NOT NULL REFERENCES athletes(id)      ON DELETE RESTRICT,
    team_id                 uuid NOT NULL REFERENCES teams(id)         ON DELETE RESTRICT,
    match_id                uuid REFERENCES matches(id)                ON DELETE RESTRICT,

    reason                  text NOT NULL,

    completion_rule         sanction_completion_rule NOT NULL,
    matches_required        int,
    fine_amount             numeric(12,2),

    fine_waived_at          timestamptz,
    fine_waiver_reason      text,

    credential_withheld_at  timestamptz,

    imposed_by              uuid NOT NULL REFERENCES users(id),
    imposed_at              timestamptz NOT NULL DEFAULT now(),

    lifted_at               timestamptz,
    lift_reason             text,

    voided_at               timestamptz,
    void_reason             text,

    created_at              timestamptz NOT NULL DEFAULT now(),
    updated_at              timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_sanctions_reason_not_blank CHECK (btrim(reason) <> ''),

    CONSTRAINT ck_sanctions_matches_required_paired CHECK (
        (completion_rule IN ('matches', 'both')) = (matches_required IS NOT NULL)),
    CONSTRAINT ck_sanctions_matches_required_positive CHECK (
        matches_required IS NULL OR matches_required > 0),

    CONSTRAINT ck_sanctions_fine_amount_paired CHECK (
        (completion_rule IN ('fine', 'both')) = (fine_amount IS NOT NULL)),
    CONSTRAINT ck_sanctions_fine_amount_positive CHECK (
        fine_amount IS NULL OR fine_amount > 0),

    CONSTRAINT ck_sanctions_fine_waiver_reason CHECK (
        (fine_waived_at IS NULL) = (fine_waiver_reason IS NULL)),
    -- A waiver forgives a fine; there has to be one to forgive.
    CONSTRAINT ck_sanctions_fine_waiver_requires_fine CHECK (
        fine_waived_at IS NULL OR fine_amount IS NOT NULL),

    CONSTRAINT ck_sanctions_lift_reason CHECK (
        (lifted_at IS NULL) = (lift_reason IS NULL)),
    CONSTRAINT ck_sanctions_void_reason CHECK (
        (voided_at IS NULL) = (void_reason IS NULL))
);

CREATE INDEX idx_sanctions_competition ON sanctions(competition_id);
CREATE INDEX idx_sanctions_athlete ON sanctions(athlete_id);

-- What SanctionProgress reads to count a team's matches served since each
-- of its sanctions was imposed.
CREATE INDEX idx_sanctions_team ON sanctions(team_id);

COMMENT ON COLUMN sanctions.fine_amount IS
  'In bolivianos. Same single-currency reasoning as tuition_charges.amount
   -- no currency column here either.';

COMMENT ON COLUMN sanctions.credential_withheld_at IS
  'The physical card held back, not the document revoked -- see this
   migration''s own header. issued_documents.status is untouched by this.';

ALTER TABLE sanctions ENABLE ROW LEVEL SECURITY;
ALTER TABLE sanctions FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON sanctions
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON sanctions TO sportfrog_app;

-- Deliberately no grant to sportfrog_public. Whether somebody is sanctioned,
-- and why, is exactly the disciplinary detail the public verification
-- endpoint (Features.Public.VerifyDocument) was built never to say (RNF-16)
-- -- see its own remarks on the reason a document was revoked for the same
-- principle applied here.

CREATE TRIGGER trg_sanctions_touch BEFORE UPDATE ON sanctions
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

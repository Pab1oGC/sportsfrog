-- =============================================================================
-- AddTuitionCharges
--
-- One month's pension, for one enrollment. No status column at all —
-- SportFrog.Domain.School.TuitionLedger is the one place that decides
-- Pending/Partial/Paid/Exonerated/Voided, computed fresh from this row's
-- own amount against whatever counts as paid, every time it is asked.
-- Writing a status here would let somebody mark a charge "pagada" by hand
-- and leave it there after a payment was later voided from it — exactly
-- the drift a derived value cannot have.
--
-- amount is bolivianos only, same as courses.monthly_fee and
-- course_enrollments.fee_override — see either column's own comment for
-- why there is no currency column to say so. Copied from whichever of
-- those two was in force the moment the charge was raised
-- (TuitionAccrual), never read fresh afterward: a price change, or a
-- scholarship granted later, must not reach back and move a charge
-- already on the books.
--
-- exonerated_at/exoneration_reason and voided_at/void_reason are each
-- paired the same way ck_course_enrollments_fee_override_reason already
-- pairs an amount with why it exists — an operator's own recorded
-- decision, with a reason, never inferred from anything else on the row.
-- Both are independent toggles rather than one "struck" flag: a charge
-- can be exonerated and later voided outright (the scholarship it forgave
-- turned out to belong to an enrollment that should never have been
-- charged at all), and TuitionLedger already resolves that combination —
-- voided outranks exonerated — without this table needing to forbid it.
-- =============================================================================

CREATE TABLE tuition_charges (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id              uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    enrollment_id       uuid NOT NULL REFERENCES course_enrollments(id) ON DELETE RESTRICT,

    -- Always the first of its month. TuitionAccrual normalizes to this
    -- before it ever reaches a row; the check is the database refusing to
    -- trust that it always will.
    period              date NOT NULL,

    amount              numeric(12,2) NOT NULL,
    due_on              date NOT NULL,

    exonerated_at       timestamptz,
    exoneration_reason  text,

    voided_at           timestamptz,
    void_reason         text,

    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_tuition_charges_period_is_month_start CHECK (period = date_trunc('month', period)::date),
    CONSTRAINT ck_tuition_charges_amount_non_negative CHECK (amount >= 0),
    CONSTRAINT ck_tuition_charges_exoneration_reason CHECK (
        (exonerated_at IS NULL) = (exoneration_reason IS NULL)),
    CONSTRAINT ck_tuition_charges_void_reason CHECK (
        (voided_at IS NULL) = (void_reason IS NULL))
);

-- One charge per enrollment per month. What TuitionAccrual's idempotency
-- rests on, the same shape uq_course_sessions_course_date_time rests a
-- calendar's own idempotency on.
CREATE UNIQUE INDEX uq_tuition_charges_enrollment_period ON tuition_charges(enrollment_id, period);
CREATE INDEX idx_tuition_charges_org_period ON tuition_charges(org_id, period);

COMMENT ON COLUMN tuition_charges.amount IS
  'In bolivianos. Every organization this module serves bills in no other
   currency, so there is deliberately no currency column -- see
   courses.monthly_fee''s own comment for the reasoning in full.';

ALTER TABLE tuition_charges ENABLE ROW LEVEL SECURITY;
ALTER TABLE tuition_charges FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON tuition_charges
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON tuition_charges TO sportfrog_app;

CREATE TRIGGER trg_tuition_charges_touch BEFORE UPDATE ON tuition_charges
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

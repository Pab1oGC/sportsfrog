-- =============================================================================
-- AddTuitionPayments
--
-- One payment, against one charge. A charge can have several -- two partial
-- payments that together cover it -- which is why this hangs off
-- tuition_charges rather than adding columns there: TuitionLedger.Resolve
-- already takes a list of payments for exactly this reason.
--
-- No status column derived from anything either, same reasoning as
-- tuition_charges' own: whether a charge reads as Pending/Partial/Paid is
-- computed fresh from amount and these rows every time, never written down
-- on the charge itself.
--
-- method and receipt_key are paired the other way from exoneration/void
-- above: a cash payment is handed over in person, so there is nothing to
-- scan, and a transfer or any other method is only trusted once there is
-- something to check it against. ck_payment_receipt is the database
-- refusing to trust that RecordPayment always enforces that pairing.
--
-- review_state is independent of method's own pairing: a cash payment is
-- born 'verified' (the operator entering it *is* the verification, and
-- audit_log already names who), while a transfer or other method is born
-- 'pending' until someone checks the receipt against it. Voiding is a third,
-- separate axis again, the same shape tuition_charges.voided_at already is
-- relative to exoneration: a verified payment can still turn out to be a
-- mistake and get voided outright.
-- =============================================================================

CREATE TYPE payment_method AS ENUM ('cash', 'transfer', 'other');
CREATE TYPE receipt_review_state AS ENUM ('pending', 'verified', 'rejected');

CREATE TABLE tuition_payments (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id         uuid NOT NULL REFERENCES organizations(id)    ON DELETE CASCADE,
    charge_id      uuid NOT NULL REFERENCES tuition_charges(id)  ON DELETE RESTRICT,

    amount         numeric(12,2) NOT NULL,
    method         payment_method NOT NULL,

    -- A key into object storage, never the file itself -- same reasoning
    -- athletes.photo_key and courses' own pictures already follow.
    receipt_key    text,

    review_state    receipt_review_state NOT NULL,
    review_reason   text,
    reviewed_at     timestamptz,

    voided_at      timestamptz,
    void_reason    text,

    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_tuition_payments_amount_positive CHECK (amount > 0),
    CONSTRAINT ck_payment_receipt CHECK ((method = 'cash') = (receipt_key IS NULL)),
    CONSTRAINT ck_tuition_payments_review_reason CHECK (
        (review_state = 'rejected') = (review_reason IS NOT NULL)),
    CONSTRAINT ck_tuition_payments_void_reason CHECK (
        (voided_at IS NULL) = (void_reason IS NULL))
);

CREATE INDEX idx_tuition_payments_charge ON tuition_payments(charge_id);

-- The receipts tray reads exactly this slice -- org-wide, not per course
-- (see RecordPayment's own remarks for why these endpoints take no course
-- scope at all).
CREATE INDEX idx_tuition_payments_pending_review ON tuition_payments(org_id)
    WHERE review_state = 'pending' AND voided_at IS NULL;

COMMENT ON COLUMN tuition_payments.amount IS
  'In bolivianos. Same single-currency reasoning as tuition_charges.amount
   and courses.monthly_fee -- no currency column here either.';

ALTER TABLE tuition_payments ENABLE ROW LEVEL SECURITY;
ALTER TABLE tuition_payments FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON tuition_payments
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON tuition_payments TO sportfrog_app;

CREATE TRIGGER trg_tuition_payments_touch BEFORE UPDATE ON tuition_payments
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

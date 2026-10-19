-- =============================================================================
-- AddSanctionPayments
--
-- One payment, against one sanction's fine. A fine can have several of
-- these -- two partial payments that together cover it -- which is why this
-- hangs off sanctions rather than adding columns there, exactly the same
-- reasoning tuition_payments already follows relative to tuition_charges.
--
-- No new enums: method and review_state reuse payment_method and
-- receipt_review_state verbatim. A payment is a payment whether it clears a
-- pension or a fine -- the method somebody paid by and whether a receipt has
-- been checked do not change meaning depending on what it is paying off, and
-- a second pair of identical labels would be a fork waiting to drift from
-- this one.
--
-- No status column either, same reasoning again: SanctionLedger.Resolve
-- already takes a finePaid figure to compute from, computed fresh every
-- time from whichever of these rows still count -- see ReadSanctions for
-- where that is actually applied.
-- =============================================================================

CREATE TABLE sanction_payments (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id         uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    sanction_id    uuid NOT NULL REFERENCES sanctions(id)     ON DELETE RESTRICT,

    amount         numeric(12,2) NOT NULL,
    method         payment_method NOT NULL,

    -- A key into object storage, never the file itself -- same reasoning
    -- tuition_payments.receipt_key already follows.
    receipt_key    text,

    review_state    receipt_review_state NOT NULL,
    review_reason   text,
    reviewed_at     timestamptz,

    voided_at      timestamptz,
    void_reason    text,

    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_sanction_payments_amount_positive CHECK (amount > 0),
    CONSTRAINT ck_sanction_payment_receipt CHECK ((method = 'cash') = (receipt_key IS NULL)),
    CONSTRAINT ck_sanction_payments_review_reason CHECK (
        (review_state = 'rejected') = (review_reason IS NOT NULL)),
    CONSTRAINT ck_sanction_payments_void_reason CHECK (
        (voided_at IS NULL) = (void_reason IS NULL))
);

CREATE INDEX idx_sanction_payments_sanction ON sanction_payments(sanction_id);

-- The receipts tray reads exactly this slice, scoped to one competition at
-- a time through sanctions.competition_id -- see ReadPendingSanctionReceipts.
CREATE INDEX idx_sanction_payments_pending_review ON sanction_payments(org_id)
    WHERE review_state = 'pending' AND voided_at IS NULL;

COMMENT ON COLUMN sanction_payments.amount IS
  'In bolivianos. Same single-currency reasoning as sanctions.fine_amount
   and tuition_payments.amount -- no currency column here either.';

ALTER TABLE sanction_payments ENABLE ROW LEVEL SECURITY;
ALTER TABLE sanction_payments FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON sanction_payments
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON sanction_payments TO sportfrog_app;

-- Deliberately no grant to sportfrog_public, same as sanctions itself: how a
-- fine was paid is exactly the kind of disciplinary detail the public
-- verification endpoint was built never to say.

CREATE TRIGGER trg_sanction_payments_touch BEFORE UPDATE ON sanction_payments
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

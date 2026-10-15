-- =============================================================================
-- AddTuitionPayments - rollback
--
-- Drops the table and its two enums whole. Nothing outside this migration
-- yet reads or writes any of them.
-- =============================================================================

DROP TRIGGER IF EXISTS trg_tuition_payments_touch ON tuition_payments;

DROP TABLE IF EXISTS tuition_payments;

DROP TYPE IF EXISTS receipt_review_state;
DROP TYPE IF EXISTS payment_method;

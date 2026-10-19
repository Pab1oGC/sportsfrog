-- =============================================================================
-- AddSanctionPayments - rollback
--
-- Drops the table whole. No enum to drop -- payment_method and
-- receipt_review_state belong to AddTuitionPayments, reused here as they
-- are.
-- =============================================================================

DROP TRIGGER IF EXISTS trg_sanction_payments_touch ON sanction_payments;

DROP TABLE IF EXISTS sanction_payments;

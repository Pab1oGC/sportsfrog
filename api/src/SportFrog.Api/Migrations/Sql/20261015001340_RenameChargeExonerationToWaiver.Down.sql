-- =============================================================================
-- RenameChargeExonerationToWaiver - rollback
-- =============================================================================

ALTER TABLE tuition_charges RENAME CONSTRAINT ck_tuition_charges_waiver_reason
    TO ck_tuition_charges_exoneration_reason;

ALTER TABLE tuition_charges RENAME COLUMN waiver_reason TO exoneration_reason;
ALTER TABLE tuition_charges RENAME COLUMN waived_at TO exonerated_at;

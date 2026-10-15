-- =============================================================================
-- RenameChargeExonerationToWaiver
--
-- "Exonerar" read as an unfamiliar word to the people actually using this
-- screen. "Condonar" (waive, in the code) is the same concept -- forgiving
-- a charge that was correctly raised but is not owed after all -- under
-- the term staff actually reach for. A rename only: nothing about the
-- column's own rules (the reason-pairing check, the independence from
-- voided_at) changes, so no data moves and no other column is touched.
-- =============================================================================

ALTER TABLE tuition_charges RENAME COLUMN exonerated_at TO waived_at;
ALTER TABLE tuition_charges RENAME COLUMN exoneration_reason TO waiver_reason;

ALTER TABLE tuition_charges RENAME CONSTRAINT ck_tuition_charges_exoneration_reason
    TO ck_tuition_charges_waiver_reason;

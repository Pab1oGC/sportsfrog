-- =============================================================================
-- AddTuitionCharges - rollback
--
-- Drops the table whole. Nothing outside this migration yet reads or
-- writes it.
-- =============================================================================

DROP TRIGGER IF EXISTS trg_tuition_charges_touch ON tuition_charges;

DROP TABLE IF EXISTS tuition_charges;

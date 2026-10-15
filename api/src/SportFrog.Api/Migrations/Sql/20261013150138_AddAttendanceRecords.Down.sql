-- =============================================================================
-- AddAttendanceRecords - rollback
--
-- Drops the table and its enum whole. Nothing outside this migration yet
-- reads or writes either one.
-- =============================================================================

DROP TRIGGER IF EXISTS trg_attendance_records_touch ON attendance_records;

DROP TABLE IF EXISTS attendance_records;

DROP TYPE IF EXISTS attendance_state;

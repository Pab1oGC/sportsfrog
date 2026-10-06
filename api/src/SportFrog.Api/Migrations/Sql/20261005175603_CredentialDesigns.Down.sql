-- Reverses CredentialDesigns. Competitions lose their design choice, which is
-- the only thing this migration attached to them.

DROP INDEX IF EXISTS idx_competitions_credential_design;

ALTER TABLE competitions DROP COLUMN IF EXISTS credential_design_id;

DROP TABLE IF EXISTS credential_designs;

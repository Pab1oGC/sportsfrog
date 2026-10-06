-- =============================================================================
-- AddCredentialIssuance — rollback
--
-- Restoring template_id/template_version to NOT NULL will fail if any
-- credential batch or issued document was written while this migration was
-- applied — by design: a rollback is a deliberate operation, not one meant to
-- silently discard rows that only make sense under the schema being removed.
-- =============================================================================

DROP TABLE IF EXISTS credential_number_counters;

ALTER TABLE issued_documents DROP CONSTRAINT IF EXISTS ck_issued_documents_certificate_has_no_visible_id;
ALTER TABLE issued_documents DROP CONSTRAINT IF EXISTS ck_issued_documents_has_a_design;
ALTER TABLE issued_documents DROP COLUMN IF EXISTS visible_id;
ALTER TABLE issued_documents ALTER COLUMN template_version SET NOT NULL;
ALTER TABLE issued_documents ALTER COLUMN template_id SET NOT NULL;

ALTER TABLE document_batches DROP CONSTRAINT IF EXISTS ck_document_batches_certificate_has_no_snapshot;
ALTER TABLE document_batches DROP CONSTRAINT IF EXISTS ck_document_batches_has_a_design;
ALTER TABLE document_batches DROP COLUMN IF EXISTS credential_snapshot;
ALTER TABLE document_batches ALTER COLUMN template_version SET NOT NULL;
ALTER TABLE document_batches ALTER COLUMN template_id SET NOT NULL;

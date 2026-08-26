-- =============================================================================
-- AddDocumentBatches - rollback
--
-- The issued documents stay, and so do their PDFs in object storage. Only the
-- record of which request produced them goes: a credential is a fact about a
-- person, and rolling back a deployment is not a reason to stop believing it.
-- =============================================================================

DROP INDEX IF EXISTS idx_issued_batch;

ALTER TABLE issued_documents DROP COLUMN IF EXISTS batch_id;

DROP TRIGGER IF EXISTS trg_document_batches_touch ON document_batches;

DROP TABLE IF EXISTS document_batches;

DROP TYPE IF EXISTS document_batch_state;

COMMENT ON COLUMN issued_documents.pdf_url IS NULL;

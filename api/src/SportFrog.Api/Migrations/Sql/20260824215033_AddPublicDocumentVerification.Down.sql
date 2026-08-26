-- =============================================================================
-- AddPublicDocumentVerification - rollback
--
-- Removes the way in. The documents and their serials stay exactly as they
-- are: what is printed on a card does not become untrue because a deployment
-- was rolled back, and the cards are still out there.
-- =============================================================================

DROP FUNCTION IF EXISTS resolve_public_document(citext, text);

DROP INDEX IF EXISTS idx_issued_serial_lookup;

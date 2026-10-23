-- =============================================================================
-- AddCredentialRequests - rollback
-- =============================================================================

DROP TRIGGER IF EXISTS trg_credential_requests_touch ON credential_requests;

DROP TABLE IF EXISTS credential_requests;

DROP TYPE IF EXISTS credential_request_state;

-- =============================================================================
-- AddCredentialRequestReview - rollback
-- =============================================================================

DROP FUNCTION IF EXISTS credential_request_org(uuid);
DROP FUNCTION IF EXISTS read_pending_credential_requests();

DROP POLICY IF EXISTS platform_review ON credential_requests;

ALTER TABLE credential_requests
    DROP COLUMN IF EXISTS batch_id,
    DROP COLUMN IF EXISTS rejection_reason,
    DROP COLUMN IF EXISTS reviewed_at,
    DROP COLUMN IF EXISTS reviewed_by;

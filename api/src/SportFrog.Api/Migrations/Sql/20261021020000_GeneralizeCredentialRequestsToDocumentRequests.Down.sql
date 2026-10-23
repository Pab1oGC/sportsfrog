-- =============================================================================
-- GeneralizeCredentialRequestsToDocumentRequests - rollback
--
-- Undoes the .Up.sql file in reverse order. Any certificate request raised
-- after this migration loses the columns that said so -- the same
-- data-loss-on-rollback this schema already accepts elsewhere (see
-- AddCredentialRequestReview's own rollback for reviewed_by and the rest).
-- =============================================================================

DROP FUNCTION IF EXISTS document_request_org(uuid);
DROP FUNCTION IF EXISTS read_pending_document_requests();

ALTER TABLE document_requests
    DROP CONSTRAINT IF EXISTS ck_document_requests_credential_has_no_template_or_type,
    DROP CONSTRAINT IF EXISTS ck_document_requests_certificate_has_template_and_type,
    DROP COLUMN IF EXISTS certificate_type,
    DROP COLUMN IF EXISTS template_id,
    DROP COLUMN IF EXISTS kind;

ALTER TABLE document_requests
    RENAME CONSTRAINT document_requests_batch_id_fkey TO credential_requests_batch_id_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT document_requests_reviewed_by_fkey TO credential_requests_reviewed_by_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT document_requests_requested_by_fkey TO credential_requests_requested_by_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT document_requests_team_id_fkey TO credential_requests_team_id_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT document_requests_category_id_fkey TO credential_requests_category_id_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT document_requests_competition_id_fkey TO credential_requests_competition_id_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT document_requests_org_id_fkey TO credential_requests_org_id_fkey;
ALTER TABLE document_requests RENAME CONSTRAINT document_requests_pkey TO credential_requests_pkey;

ALTER TRIGGER trg_document_requests_touch ON document_requests RENAME TO trg_credential_requests_touch;
ALTER INDEX idx_document_requests_org RENAME TO idx_credential_requests_org;
ALTER TYPE document_request_state RENAME TO credential_request_state;
ALTER TABLE document_requests RENAME TO credential_requests;

CREATE OR REPLACE FUNCTION read_pending_credential_requests()
RETURNS TABLE (
    id             uuid,
    org_id         uuid,
    org_name       text,
    org_slug       text,
    competition_id uuid,
    category_id    uuid,
    team_id        uuid,
    valid_from     date,
    valid_to       date,
    requested_by   uuid,
    created_at     timestamptz
)
LANGUAGE sql SECURITY DEFINER STABLE AS $fn$
    SELECT
        r.id, r.org_id, o.name, o.slug::text,
        r.competition_id, r.category_id, r.team_id,
        r.valid_from, r.valid_to, r.requested_by, r.created_at
    FROM credential_requests r
    JOIN organizations o ON o.id = r.org_id
    WHERE r.status = 'pending'
    ORDER BY r.created_at;
$fn$;

REVOKE ALL ON FUNCTION read_pending_credential_requests() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION read_pending_credential_requests() TO sportfrog_app;

CREATE OR REPLACE FUNCTION credential_request_org(p_id uuid)
RETURNS TABLE (org_id uuid)
LANGUAGE sql SECURITY DEFINER STABLE AS $fn$
    SELECT org_id FROM credential_requests WHERE id = p_id;
$fn$;

REVOKE ALL ON FUNCTION credential_request_org(uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION credential_request_org(uuid) TO sportfrog_app;

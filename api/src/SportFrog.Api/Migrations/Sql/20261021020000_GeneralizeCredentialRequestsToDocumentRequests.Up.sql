-- =============================================================================
-- GeneralizeCredentialRequestsToDocumentRequests
--
-- credential_requests only ever held a credential's own ask: a competition,
-- optionally narrowed, and a validity window. Now that CreateTemplate gates
-- certificate authorship to the platform organization too (see
-- AddCertificateTemplateCatalog and AddPlatformTemplateLookup), an
-- organization asking for a batch of certificates is the exact same
-- lifecycle credential_requests already named — pending, then approved,
-- rejected or cancelled — about a different document. document_batches and
-- issued_documents already carry both kinds under one roof with a `kind`
-- column; this is that same shape, not a new one, so this migration renames
-- rather than duplicates.
--
-- kind backfills every existing row as 'credential', which is a fact and
-- not a default: every row here today is a credential request, since
-- nothing before this migration could ask for anything else. The default
-- is dropped once that backfill is done — every request raised from here
-- on has to say which kind it is, the same way document_batches.kind always
-- has.
--
-- template_id and certificate_type are the certificate half of the ask,
-- required for one kind and forbidden for the other — the same two-way
-- CHECK document_batches already carries for credential_snapshot, mirrored
-- here instead of invented fresh.
--
-- The index, the trigger and the two functions below are recreated under
-- matching names for the same reason RenameChargeExonerationToWaiver
-- renamed what it touched rather than leaving a `credential_requests`-named
-- trigger on a `document_requests` table: a name that stops matching what
-- it is attached to is confusing exactly when somebody is reading the
-- schema to understand what is attached to what. tenant_isolation and
-- platform_review are NOT recreated — both are tied to the table by its
-- identity, not its name, and follow the rename on their own; only the two
-- functions need it, because a `LANGUAGE sql` function's body is re-parsed
-- against the catalog on every call and would start failing the moment the
-- name it was written against stops resolving.
-- =============================================================================

DROP FUNCTION IF EXISTS credential_request_org(uuid);
DROP FUNCTION IF EXISTS read_pending_credential_requests();

ALTER TABLE credential_requests RENAME TO document_requests;
ALTER TYPE credential_request_state RENAME TO document_request_state;
ALTER INDEX idx_credential_requests_org RENAME TO idx_document_requests_org;
ALTER TRIGGER trg_credential_requests_touch ON document_requests RENAME TO trg_document_requests_touch;

-- RENAME TABLE does not rename the constraints it left auto-named after the
-- old table; RenameChargeExonerationToWaiver's own RENAME CONSTRAINT is the
-- precedent followed here.
ALTER TABLE document_requests RENAME CONSTRAINT credential_requests_pkey TO document_requests_pkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT credential_requests_org_id_fkey TO document_requests_org_id_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT credential_requests_competition_id_fkey TO document_requests_competition_id_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT credential_requests_category_id_fkey TO document_requests_category_id_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT credential_requests_team_id_fkey TO document_requests_team_id_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT credential_requests_requested_by_fkey TO document_requests_requested_by_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT credential_requests_reviewed_by_fkey TO document_requests_reviewed_by_fkey;
ALTER TABLE document_requests
    RENAME CONSTRAINT credential_requests_batch_id_fkey TO document_requests_batch_id_fkey;

ALTER TABLE document_requests
    ADD COLUMN kind document_kind NOT NULL DEFAULT 'credential';

ALTER TABLE document_requests
    ALTER COLUMN kind DROP DEFAULT;

ALTER TABLE document_requests
    ADD COLUMN template_id uuid REFERENCES document_templates(id) ON DELETE RESTRICT,
    ADD COLUMN certificate_type text;

ALTER TABLE document_requests
    ADD CONSTRAINT ck_document_requests_certificate_has_template_and_type
    CHECK (kind <> 'certificate' OR (template_id IS NOT NULL AND certificate_type IS NOT NULL));

ALTER TABLE document_requests
    ADD CONSTRAINT ck_document_requests_credential_has_no_template_or_type
    CHECK (kind <> 'credential' OR (template_id IS NULL AND certificate_type IS NULL));

COMMENT ON COLUMN document_requests.template_id IS
  'The design asked for. Null for a credential, which has none to name —
   mirrors document_batches.template_id''s own remarks.';

COMMENT ON COLUMN document_requests.certificate_type IS
  'Why a certificate is asked for. Null for a credential; required for a
   certificate by ck_document_requests_certificate_has_template_and_type.';

CREATE OR REPLACE FUNCTION read_pending_document_requests()
RETURNS TABLE (
    id               uuid,
    org_id           uuid,
    org_name         text,
    org_slug         text,
    -- Cast to text, the same reason org_slug casts citext: a column read
    -- through a plain DbDataReader asks for a type ADO.NET already knows,
    -- not a type only this database defines.
    kind             text,
    template_id      uuid,
    certificate_type text,
    competition_id   uuid,
    category_id      uuid,
    team_id          uuid,
    valid_from       date,
    valid_to         date,
    requested_by     uuid,
    created_at       timestamptz
)
LANGUAGE sql SECURITY DEFINER STABLE AS $fn$
    SELECT
        r.id, r.org_id, o.name, o.slug::text,
        r.kind::text, r.template_id, r.certificate_type,
        r.competition_id, r.category_id, r.team_id,
        r.valid_from, r.valid_to, r.requested_by, r.created_at
    FROM document_requests r
    JOIN organizations o ON o.id = r.org_id
    WHERE r.status = 'pending'
    ORDER BY r.created_at;
$fn$;

REVOKE ALL ON FUNCTION read_pending_document_requests() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION read_pending_document_requests() TO sportfrog_app;

CREATE OR REPLACE FUNCTION document_request_org(p_id uuid)
RETURNS TABLE (org_id uuid)
LANGUAGE sql SECURITY DEFINER STABLE AS $fn$
    SELECT org_id FROM document_requests WHERE id = p_id;
$fn$;

REVOKE ALL ON FUNCTION document_request_org(uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION document_request_org(uuid) TO sportfrog_app;

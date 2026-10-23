-- =============================================================================
-- AddCredentialRequests
--
-- RequestDocumentBatch now refuses a credential (not a certificate) to
-- anyone but the platform organization: see PlatformAuthority and its own
-- gate. An organization that used to request its own credentials still
-- needs a way to ask for them, with none of the authority to start a batch
-- itself.
--
-- This table is that ask. It holds the same facts a credential batch needs
-- — competition, and optionally a category or team to narrow it, and a
-- validity window — and nothing that prints anything. Approving one is a
-- later step that will raise a document_batches row from it; this
-- migration only adds the ask and its lifecycle, Pending through either
-- Approved, Rejected or Cancelled.
--
-- No batch_id or reviewer columns yet: they belong to the approval step,
-- not to this one, the same way document_batches.batch_id on
-- issued_documents arrived in its own later migration rather than with the
-- table it points at.
-- =============================================================================

CREATE TYPE credential_request_state AS ENUM ('pending', 'approved', 'rejected', 'cancelled');

CREATE TABLE credential_requests (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id         uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    competition_id uuid NOT NULL REFERENCES competitions(id) ON DELETE RESTRICT,
    category_id    uuid REFERENCES categories(id) ON DELETE RESTRICT,
    team_id        uuid REFERENCES teams(id) ON DELETE RESTRICT,
    valid_from     date,
    valid_to       date,
    status         credential_request_state NOT NULL DEFAULT 'pending',
    requested_by   uuid NOT NULL REFERENCES users(id),
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    cancelled_at   timestamptz
);

CREATE INDEX idx_credential_requests_org ON credential_requests(org_id, created_at DESC);

COMMENT ON COLUMN credential_requests.status IS
  'pending until SportFrog looks at it. Then approved (a document_batches row
   was raised from it) or rejected — both final, and both the platform
   organization''s decision, not the asking organization''s — or cancelled,
   the one transition the asking organization makes itself, and only while
   still pending.';

ALTER TABLE credential_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE credential_requests FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON credential_requests
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON credential_requests TO sportfrog_app;

-- Nothing here is public, the same reasoning document_batches already
-- follows: what an organization asked for is its own business (RNF-16).

CREATE TRIGGER trg_credential_requests_touch BEFORE UPDATE ON credential_requests
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

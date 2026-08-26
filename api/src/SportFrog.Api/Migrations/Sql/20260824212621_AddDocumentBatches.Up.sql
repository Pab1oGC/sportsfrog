-- =============================================================================
-- AddDocumentBatches
--
-- Printing a season's credentials is the heaviest thing this system does. Each
-- card decodes a photograph, draws a layout over artwork, builds a QR code and
-- composes a PDF; four hundred of them is minutes. Like a batch of
-- photographs, the request is accepted and the work happens afterwards, so
-- there has to be a row saying it is happening and what became of it.
--
-- What became of each subject is deliberately not all in one place. A card
-- that was printed is a row in issued_documents — it is a fact about a person
-- being accredited, not a line in a progress report — and it points back at
-- the batch it came from. Only the subjects that could NOT be issued live in
-- this table, because there is nowhere else for them to be.
-- =============================================================================

CREATE TYPE document_batch_state AS ENUM ('queued', 'running', 'finished', 'failed');

CREATE TABLE document_batches (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id           uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    kind             document_kind NOT NULL,
    template_id      uuid NOT NULL REFERENCES document_templates(id) ON DELETE RESTRICT,
    template_version integer      NOT NULL,
    competition_id   uuid NOT NULL REFERENCES competitions(id) ON DELETE RESTRICT,
    category_id      uuid REFERENCES categories(id) ON DELETE RESTRICT,
    team_id          uuid REFERENCES teams(id) ON DELETE RESTRICT,
    certificate_type text,
    valid_from       date,
    valid_to         date,
    status           document_batch_state NOT NULL DEFAULT 'queued',
    total            integer     NOT NULL DEFAULT 0,
    issued           integer     NOT NULL DEFAULT 0,
    skipped          integer     NOT NULL DEFAULT 0,
    sheet_key        text,
    problems         jsonb       NOT NULL DEFAULT '[]'::jsonb,
    failure          text,
    requested_by     uuid NOT NULL REFERENCES users(id),
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now(),
    finished_at      timestamptz,

    -- A batch prints from one design, and the design it printed from has to
    -- still be readable afterwards. This is the same guarantee an issued
    -- document has, applied to the request that produced it.
    CONSTRAINT fk_batch_template_version
        FOREIGN KEY (template_id, template_version)
        REFERENCES document_template_versions(template_id, version)
        ON DELETE RESTRICT
);

CREATE INDEX idx_document_batches_org ON document_batches(org_id, created_at DESC);

COMMENT ON COLUMN document_batches.sheet_key IS
  'The imposition: every card of the batch laid several to a sheet with cut
   marks, in object storage. This is what actually goes to a printer — the
   individual PDFs are for sending one person their document, which is a
   different act.';

COMMENT ON COLUMN document_batches.problems IS
  $comment$Subjects that could not be issued, one entry each:
[
  { "subject": "Gomez Pedro", "reason": "no_photo" },
  { "subject": "Diaz Ana",    "reason": "already_issued" }
]
Names the subject rather than only counting it: "seven skipped" is not
something an operator can act on, and "Pedro has no photograph" is.$comment$;

COMMENT ON COLUMN document_batches.skipped IS
  'Subjects the batch did not print. Not failures of the system — somebody
   with no photograph on a design that prints one, somebody who already holds
   a valid credential. Counted apart from issued so the two numbers add up to
   total.';

ALTER TABLE document_batches ENABLE ROW LEVEL SECURITY;
ALTER TABLE document_batches FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON document_batches
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON document_batches TO sportfrog_app;

-- Nothing here is public. What a competition printed, and for whom, is the
-- organization's business (RNF-16).

CREATE TRIGGER trg_document_batches_touch BEFORE UPDATE ON document_batches
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- =============================================================================
-- Which batch a document came out of
--
-- Nullable, because not every document comes from one: reissuing a single
-- credential to somebody who lost theirs is one card and one act, with no
-- batch behind it.
-- =============================================================================

ALTER TABLE issued_documents
    ADD COLUMN batch_id uuid REFERENCES document_batches(id) ON DELETE SET NULL;

CREATE INDEX idx_issued_batch ON issued_documents(batch_id) WHERE batch_id IS NOT NULL;

COMMENT ON COLUMN issued_documents.batch_id IS
  'The batch that printed it, when it came from one. Set to null if the batch
   record is ever removed: the document outlives the request that produced it,
   and losing the provenance is not a reason to lose the credential.';

COMMENT ON COLUMN issued_documents.pdf_url IS
  'Key of the document''s own PDF in object storage, under the organization''s
   prefix. Not a URL despite the name, which predates object storage: what a
   reader is handed is a signed link built at read time, and it expires.';

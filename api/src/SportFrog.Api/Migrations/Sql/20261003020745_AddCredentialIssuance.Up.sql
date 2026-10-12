-- =============================================================================
-- AddCredentialIssuance
--
-- Lets a document batch and an issued document be a credential — printed
-- from the fixed structure and the accreditation catalogue, not from a
-- document_templates row — rather than only ever a certificate.
--
-- template_id and template_version were NOT NULL because every document used
-- to have a design to point to. A decreed credential does not: its structure
-- never changes, so there is nothing for a template row to describe. Both
-- columns become nullable, and a CHECK constraint on each table takes over
-- part of the guarantee NOT NULL used to give for free.
--
-- In its place, a credential batch requested from here on pins
-- credential_snapshot: the accreditation catalogue's codes, names and
-- colours, and the organization's legal notice, exactly as they stood the
-- moment the batch was requested. This is the same promise TemplateVersion
-- makes for a certificate, applied to the one thing about a credential that
-- is still a design decision somebody can change later — see
-- credential_snapshot's own comment, and CredentialSnapshot in the domain
-- project, for why who holds which category is deliberately NOT part of
-- what gets frozen.
--
-- The CHECK below is phrased as "a template or a snapshot", not "a
-- credential has a snapshot", on purpose: this organization's data already
-- holds credential batches and issued documents printed before this decree,
-- back when a credential was exactly as template-driven as a certificate
-- still is. Those rows carry a real template_id and template_version and no
-- snapshot at all, and they are a true record of cards that were actually
-- printed — not a defect a CHECK constraint gets to reject on its way in.
-- The invariant this schema can actually promise is that every batch and
-- every issued document points at *something* it was composed from, old
-- style or new.
--
-- visible_id is the identifier printed in a credential's data block — a
-- delegation code and a sequential number, read by a steward against a
-- printed list. Unlike the QR's serial, it is sequential on purpose, which
-- is exactly why it needs credential_number_counters: claiming "the next
-- number for this competition" has to be atomic across batches that could
-- run at the same time, the same hazard NextSerial's in-batch set guards
-- against for a single batch's own serials.
-- =============================================================================

-- -----------------------------------------------------------------------
-- document_batches
-- -----------------------------------------------------------------------

ALTER TABLE document_batches ALTER COLUMN template_id DROP NOT NULL;
ALTER TABLE document_batches ALTER COLUMN template_version DROP NOT NULL;

ALTER TABLE document_batches ADD COLUMN credential_snapshot jsonb;

-- Every batch was composed from a template version (every certificate, and
-- every credential requested before this decree) or a frozen catalogue
-- snapshot (every credential requested after it). Never neither.
ALTER TABLE document_batches
    ADD CONSTRAINT ck_document_batches_has_a_design
    CHECK ((template_id IS NOT NULL AND template_version IS NOT NULL) OR credential_snapshot IS NOT NULL);

-- A certificate never gets a credential-shaped design: it prints from a
-- template, full stop, both before and after this migration.
ALTER TABLE document_batches
    ADD CONSTRAINT ck_document_batches_certificate_has_no_snapshot
    CHECK (kind <> 'certificate' OR credential_snapshot IS NULL);

COMMENT ON COLUMN document_batches.credential_snapshot IS
  $comment$The accreditation catalogue and legal notice as they stood when
this credential batch was requested, so reissuing a card later reproduces the
card that was actually printed:
{
  "competition_logo_key": "orgs/<org>/competitions/<id>/logo.webp",
  "legal_text": "Esta credencial es propiedad de...",
  "items": {
    "<item-uuid>": { "kind": "zone", "code": "AZUL", "name": "Campo de juego...", "color_hex": "#1F3864", "icon_key": null }
  },
  "categories": {
    "<category-uuid>": { "code": "Aa", "name": "Deportista", "color_hex": "#1F3864" }
  }
}
Null for a certificate batch, which prints from a document_template_versions
row instead. Who holds which category is deliberately not here — see
CredentialSnapshot in the domain project.$comment$;

-- -----------------------------------------------------------------------
-- issued_documents
-- -----------------------------------------------------------------------

ALTER TABLE issued_documents ALTER COLUMN template_id DROP NOT NULL;
ALTER TABLE issued_documents ALTER COLUMN template_version DROP NOT NULL;

ALTER TABLE issued_documents ADD COLUMN visible_id text;

-- Same shape as ck_document_batches_has_a_design, for the same reason: a
-- credential issued before this decree carries a real template_id and no
-- visible_id, because the concept did not exist yet. That is history, not a
-- row to refuse.
ALTER TABLE issued_documents
    ADD CONSTRAINT ck_issued_documents_has_a_design
    CHECK ((template_id IS NOT NULL AND template_version IS NOT NULL) OR visible_id IS NOT NULL);

ALTER TABLE issued_documents
    ADD CONSTRAINT ck_issued_documents_certificate_has_no_visible_id
    CHECK (kind <> 'certificate' OR visible_id IS NULL);

COMMENT ON COLUMN issued_documents.visible_id IS
  'A delegation code and a sequential number, e.g. "IND-0042" — what a steward
   reads off the card and matches against a printed list. Sequential and
   public-facing on purpose, unlike serial_number: the two answer different
   questions (see VisibleCredentialId''s own remarks). Null for a certificate,
   which carries no such identifier, and for a credential issued before this
   decree, back when the concept did not exist yet.';

-- -----------------------------------------------------------------------
-- The counter a credential''s visible_id is claimed from
-- -----------------------------------------------------------------------

CREATE TABLE credential_number_counters (
    competition_id uuid PRIMARY KEY REFERENCES competitions(id) ON DELETE CASCADE,
    org_id         uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,

    -- The next number to hand out, not the last one given. A fresh
    -- competition starts here with no row at all; the first claim creates
    -- it, the same "insert on first use" shape UnaffiliatedClub already uses
    -- for a different per-organization singleton.
    next_number    integer NOT NULL DEFAULT 1,

    updated_at     timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE credential_number_counters IS
  'One row per competition, claimed and incremented atomically
   (INSERT ... ON CONFLICT DO UPDATE ... RETURNING) so two batches requested
   in the same instant never hand out the same visible_id. Shared across every
   club in the competition on purpose: the number is "the Nth credential this
   competition issued", not "the Nth for this club" — see
   VisibleCredentialId''s own remarks on why the two identifiers it joins
   together answer different questions.';

ALTER TABLE credential_number_counters ENABLE ROW LEVEL SECURITY;
ALTER TABLE credential_number_counters FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON credential_number_counters
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE ON credential_number_counters TO sportfrog_app;

-- Nothing here is public, and nothing deletes a row: a counter only ever
-- moves forward, the same "no DELETE and no UPDATE of history" shape
-- document_template_versions already protects for a different kind of
-- irreversible record (that one refuses UPDATE too; this one is updated by
-- design, since advancing the counter is the whole point of the table).

CREATE TRIGGER trg_credential_number_counters_touch BEFORE UPDATE ON credential_number_counters
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

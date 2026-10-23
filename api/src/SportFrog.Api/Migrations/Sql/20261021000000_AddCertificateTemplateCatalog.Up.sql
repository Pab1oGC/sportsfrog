-- =============================================================================
-- AddCertificateTemplateCatalog
--
-- A certificate template an organization did not design itself, so that it
-- has something to name on day one instead of nothing: every organization
-- starts with zero document_templates rows (CreateTemplate is the only way
-- to get one, and nothing seeds any), and once authoring a template becomes
-- the platform organization's job alone, an organization that never asked
-- for a bespoke design still needs a certificate it can request.
--
-- is_catalog marks a row as that shared design. It does not replace
-- tenant_isolation and does not grant a write of any kind across
-- organizations -- a catalog row is still owned by exactly one org_id (the
-- platform organization's, enforced in the application, the same way every
-- other row here is owned by whoever created it) and the CHECK constraint
-- below is the only new write-time rule.
--
-- The read side is the actual point, and it follows the precedent this
-- schema already set for the public portal: a second, narrower SELECT
-- policy layered on top of tenant_isolation rather than a change to it.
-- Postgres ORs permissive policies for the same role and command together,
-- so sportfrog_app keeps seeing exactly what tenant_isolation already gave
-- it (its own organization's rows) and additionally sees catalog rows no
-- matter which organization's connection is asking -- the same shape as
-- published_athletes, published_clubs and the rest of
-- ScopePublicReadsToPublishedCompetitions, aimed at sportfrog_app instead of
-- sportfrog_public. It is not the SECURITY DEFINER escape hatch
-- AddCredentialRequestReview reaches for: that one exists because a request
-- queue has to be read across every organization at once with no single
-- current_org to set; this one never leaves a single tenant_isolation
-- context, it just widens what satisfies it.
--
-- document_template_versions needs the same widening, not a lesser one: a
-- template without its current version's layout readable is a template a
-- batch can accept and then print nothing from, the exact failure mode
-- RequestDocumentBatch's own snapshot comment warns about for a certificate
-- whose design changed between asking and printing.
-- =============================================================================

ALTER TABLE document_templates
    ADD COLUMN is_catalog boolean NOT NULL DEFAULT false;

-- Only a certificate can be shared: a credential has no template of its own
-- any more (CreateTemplate refuses one), and a decreed credential's only
-- per-organization choices live in credential_designs, not here.
ALTER TABLE document_templates
    ADD CONSTRAINT ck_document_templates_catalog_is_certificate
    CHECK (NOT is_catalog OR kind = 'certificate');

COMMENT ON COLUMN document_templates.is_catalog IS
  'Whether every organization may name this design in a certificate request,
   not only the one in org_id. Never set directly by the organization that
   owns the row -- see CreateTemplate''s platform-only gate -- and narrowed
   by ck_document_templates_catalog_is_certificate to a design a credential
   could never have been anyway.';

CREATE POLICY catalog_document_templates ON document_templates
    FOR SELECT TO sportfrog_app
    USING (is_catalog AND deleted_at IS NULL);

COMMENT ON POLICY catalog_document_templates ON document_templates IS
  'Lets every organization''s connection see a catalog row alongside its own,
   the same way published_athletes lets the public portal see a roster row
   alongside nothing of its own. Additive to tenant_isolation, not a
   replacement for it: an organization still cannot see another
   organization''s bespoke design, only the platform''s own rows marked
   shared.';

CREATE POLICY catalog_document_template_versions ON document_template_versions
    FOR SELECT TO sportfrog_app
    USING (
        EXISTS (
            SELECT 1
            FROM document_templates t
            WHERE t.id = document_template_versions.template_id
              AND t.is_catalog
        )
    );

COMMENT ON POLICY catalog_document_template_versions ON document_template_versions IS
  'Without this, a batch requested against a catalog template would be
   accepted -- the template row itself is visible -- and then print nothing:
   IssueDocumentsJob reads the layout from document_template_versions, not
   from document_templates, so that is where the same visibility has to
   reach.';

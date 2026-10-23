-- =============================================================================
-- AddPlatformTemplateLookup
--
-- Lets the platform organization resolve which organization owns a
-- document_templates row, regardless of which one it is. CreateTemplate,
-- UpdateTemplate, DeleteTemplate and UploadTemplateBackground move to a
-- platform-only gate in this same change: an organization no longer designs
-- its own certificate, the platform organization does, either into its own
-- shared catalog (AddCertificateTemplateCatalog) or as a bespoke design for
-- one specific organization. Updating or deleting one by id has to find out
-- which organization that is before it can act inside it — exactly the
-- shape AddCredentialRequestReview already solved for credential_requests,
-- and solved the same way here: a narrow SECURITY DEFINER function, backed
-- by a policy that applies to nobody but the schema owner that function
-- runs as (see AllowSchemaOwnerThroughTenantIsolation for why that role is
-- safe to grant it to).
--
-- Not the catalog policy from AddCertificateTemplateCatalog: that one only
-- ever widens visibility to rows marked is_catalog, and a bespoke design
-- for another organization is never one of those by definition. This one
-- has to reach every row, the same unbounded reach credential_request_org
-- already has for its own table.
-- =============================================================================

CREATE POLICY platform_template_lookup ON document_templates
    FOR SELECT TO sportfrog_owner
    USING (true);

COMMENT ON POLICY platform_template_lookup ON document_templates IS
  'Lets document_template_org below see across every organization, not only
   catalog rows or the one app.current_org names. Scoped to sportfrog_owner
   alone -- see platform_review on credential_requests for the same
   narrowing and why it is safe.';

CREATE OR REPLACE FUNCTION document_template_org(p_id uuid)
RETURNS TABLE (org_id uuid)
LANGUAGE sql SECURITY DEFINER STABLE AS $fn$
    SELECT org_id FROM document_templates WHERE id = p_id;
$fn$;

REVOKE ALL ON FUNCTION document_template_org(uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION document_template_org(uuid) TO sportfrog_app;

-- =============================================================================
-- AddPlatformTemplateLookup - rollback
-- =============================================================================

REVOKE EXECUTE ON FUNCTION document_template_org(uuid) FROM sportfrog_app;
DROP FUNCTION document_template_org(uuid);

DROP POLICY platform_template_lookup ON document_templates;

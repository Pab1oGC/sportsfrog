-- =============================================================================
-- AddCertificateTemplateCatalog - rollback
--
-- Undoes the .Up.sql file in reverse order: drop the two new policies first,
-- then the constraint, then the column.
-- =============================================================================

DROP POLICY catalog_document_template_versions ON document_template_versions;
DROP POLICY catalog_document_templates ON document_templates;

ALTER TABLE document_templates
    DROP CONSTRAINT ck_document_templates_catalog_is_certificate;

ALTER TABLE document_templates
    DROP COLUMN is_catalog;

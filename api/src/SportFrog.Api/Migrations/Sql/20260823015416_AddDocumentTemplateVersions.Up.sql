-- =============================================================================
-- AddDocumentTemplateVersions
--
-- Makes document_templates.version mean something.
--
-- The schema already says that an issued document references a template and
-- the version of it that was used, "so the original can be reproduced on
-- reissue". That promise had nothing behind it: a layout edited in place
-- leaves no trace of what it used to be, so a credential reissued a season
-- later would come out looking like this year's design, with this year's
-- badge, for a card that was printed under the old one.
--
-- So every saved layout is kept. The template row holds the current one —
-- which is what an editor opens and what a new batch prints — and this table
-- holds every one there has ever been, including the current. Nothing here is
-- ever updated: a version is what a design looked like at a moment, and
-- moments do not change.
-- =============================================================================

CREATE TABLE document_template_versions (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id       uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    template_id  uuid NOT NULL REFERENCES document_templates(id) ON DELETE CASCADE,
    version      integer     NOT NULL,
    layout       jsonb       NOT NULL,
    page_size    text        NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now(),
    created_by   uuid REFERENCES users(id),
    UNIQUE (template_id, version)
);

CREATE INDEX idx_template_versions_org ON document_template_versions(org_id, template_id);

COMMENT ON TABLE document_template_versions IS
  'Every layout a template has ever had. Append-only: rows are written when a
   design is saved and never modified afterwards, because an issued document
   points at one of them and printing it again has to produce the same card.

   Deleted with its template, which is safe precisely because a template with
   issued documents cannot be deleted: issued_documents references it with ON
   DELETE RESTRICT.';

ALTER TABLE document_template_versions ENABLE ROW LEVEL SECURITY;
ALTER TABLE document_template_versions FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON document_template_versions
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

-- No DELETE and no UPDATE. The application cannot rewrite the history of a
-- design even by mistake, which is the only way this table is worth having.
GRANT SELECT, INSERT ON document_template_versions TO sportfrog_app;

-- The public verification of a credential (RF-45) answers whether a serial is
-- valid, not what the card looked like. Nothing here is readable anonymously.

-- No updated_at column and so no touch trigger: a row that is never updated
-- has nothing to touch.

-- An issued document names a version, so let the database be what guarantees
-- the version exists. Without this, template_version is an integer somebody
-- hopes is right.
ALTER TABLE issued_documents
    ADD CONSTRAINT fk_issued_template_version
    FOREIGN KEY (template_id, template_version)
    REFERENCES document_template_versions(template_id, version)
    ON DELETE RESTRICT;

-- =============================================================================
-- The layout column, described as it actually works now
--
-- The original comment predates object storage and spoke of background_url
-- holding an address. Backgrounds are objects now, like every other file, and
-- what the layout carries is the key of one. The value handed to a browser is
-- a signed link built when the template is read, and it expires.
-- =============================================================================

COMMENT ON COLUMN document_templates.layout IS $comment$Background image plus
fields positioned over it. Supports front and back:
{
  "front": {
    "background_key": "orgs/<org>/document-templates/<id>/<object>.jpg",
    "aspect_ratio": 1.586,
    "fields": [
      { "source":"athlete.photo",     "x":0.14, "y":0.15, "w":0.24, "h":0.45 },
      { "source":"athlete.full_name", "x":0.14, "y":0.72, "size":0.06,
        "font":"sans", "align":"left", "fit":"shrink", "min_size":0.04 },
      { "source":"document.qr",       "x":0.80, "y":0.60, "w":0.15, "h":0.15 }
    ]
  },
  "back": { "background_key": "...", "fields": [ ... ] }
}
Conventions:
  · x, y, w, h and size are expressed between 0 and 1, relative to the width
    and height of the background, so replacing the background with an image
    of a different resolution doesn't invalidate the positioning.
  · fit declares the behavior when content exceeds the space: 'shrink'
    reduces the size down to min_size, 'wrap' distributes across lines,
    'truncate' clips.
  · font is taken from a closed set the system publishes, so the browser and
    the document generator use the same typeface.
  · background_key names an object of this organization. A key belonging to
    another one is refused when the layout is saved.
Validated on the way in against a closed set of fields and properties: an
unknown property is a refusal, not something ignored, so nothing that was
never designed for can be smuggled through the column.$comment$;

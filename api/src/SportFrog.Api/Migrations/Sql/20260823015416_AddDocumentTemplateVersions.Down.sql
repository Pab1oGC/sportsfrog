-- =============================================================================
-- AddDocumentTemplateVersions - rollback
--
-- Drops the archive of layouts and the constraint that pointed at it. The
-- column comment goes back to what it said before, address and all, because
-- rolling back means the code that reads background_key is gone too.
-- =============================================================================

ALTER TABLE issued_documents DROP CONSTRAINT IF EXISTS fk_issued_template_version;

DROP TABLE IF EXISTS document_template_versions;

COMMENT ON COLUMN document_templates.layout IS $comment$Background image plus
fields positioned over it. Supports front and back:
{
  "front": {
    "background_url": "https://.../front.png",
    "aspect_ratio": 1.586,
    "fields": [
      { "source":"athlete.photo",     "x":0.14, "y":0.15, "w":0.24, "h":0.45 },
      { "source":"athlete.full_name", "x":0.14, "y":0.72, "size":0.06,
        "font":"inter", "align":"left", "fit":"shrink", "min_size":0.04 },
      { "source":"document.qr",       "x":0.80, "y":0.60, "w":0.15, "h":0.15 }
    ]
  },
  "back": { "background_url": "...", "fields": [ ... ] }
}
Conventions:
  · x, y, w, h and size are expressed between 0 and 1, relative to the width
    and height of the background, so replacing the background with an image
    of a different resolution doesn't invalidate the positioning.
  · fit declares the behavior when content exceeds the space: 'shrink'
    reduces the size down to min_size, 'wrap' distributes across lines,
    'truncate' clips.
  · font is taken from a closed set served by the system itself, so the
    browser and the document generator use the same typeface.
Validated against a schema before persisting. Does not accept executable
content.$comment$;

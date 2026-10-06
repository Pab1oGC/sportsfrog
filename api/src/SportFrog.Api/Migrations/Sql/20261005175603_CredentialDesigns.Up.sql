-- =============================================================================
-- CredentialDesigns
--
-- A credential's structure is fixed by decree, so a credential has no layout
-- to design. What an organization does get to choose is the handful of values
-- that change from one competition to the next within the same organization:
-- the legal notice on the back, the faint background drawn across the sheet,
-- the accent colour, and a fallback logo for competitions that have none.
--
-- Those values live here, once per organization, so that an organization
-- with several competitions designs its credential once and every competition
-- points at the design. A competition's own logo still wins over the fallback,
-- because the logo identifies the competition, not the organization.
--
-- The design a credential prints from is frozen into credential_snapshot when
-- the batch is requested (see CredentialSnapshot), so editing a design later
-- never changes a credential already printed.
-- =============================================================================

CREATE TABLE credential_designs (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id           uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,

    name             text NOT NULL,
    legal_text       text,
    background_key   text,
    accent_color_hex text,
    logo_key         text,

    -- At most one default per organization, kept by the application the same
    -- way document_templates.is_default is: an index would also forbid an
    -- organization from having none at all.
    is_default       boolean NOT NULL DEFAULT false,

    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT credential_designs_name_length CHECK (char_length(name) BETWEEN 1 AND 80),
    CONSTRAINT credential_designs_legal_text_length CHECK (legal_text IS NULL OR char_length(legal_text) <= 800),
    CONSTRAINT credential_designs_accent_color CHECK (accent_color_hex IS NULL OR accent_color_hex ~ '^#[0-9A-Fa-f]{6}$')
);

COMMENT ON TABLE credential_designs IS
  'The values an organization chooses for its credentials: legal notice,
   background, accent colour and fallback logo. The structure of the card is
   not here, because it is fixed by decree. Competitions point at one of these
   rows; a competition without one prints from the organization''s default.';

COMMENT ON COLUMN credential_designs.background_key IS
  'Storage key of the faint picture drawn once across the whole sheet. Null
   means the competition''s own banner is used instead, if it has one.';

COMMENT ON COLUMN credential_designs.logo_key IS
  'Storage key of the logo used when a competition has none of its own. A
   competition''s own logo always wins over this one.';

CREATE INDEX idx_credential_designs_org ON credential_designs(org_id);

ALTER TABLE credential_designs ENABLE ROW LEVEL SECURITY;
ALTER TABLE credential_designs FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON credential_designs
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON credential_designs TO sportfrog_app;

CREATE TRIGGER trg_credential_designs_touch BEFORE UPDATE ON credential_designs
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- -----------------------------------------------------------------------------
-- A competition names the design it prints from. Null means the default one of
-- its organization. Deleting a design sends its competitions back to the
-- default rather than deleting them: the design is a choice, not a fact the
-- competition cannot exist without.
-- -----------------------------------------------------------------------------

ALTER TABLE competitions
    ADD COLUMN credential_design_id uuid REFERENCES credential_designs(id) ON DELETE SET NULL;

CREATE INDEX idx_competitions_credential_design ON competitions(credential_design_id)
    WHERE credential_design_id IS NOT NULL;

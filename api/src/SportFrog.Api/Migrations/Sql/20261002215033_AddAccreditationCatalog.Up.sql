-- =============================================================================
-- AddAccreditationCatalog
--
-- What a decreed credential prints, which until now had nowhere to live.
--
-- A credential stopped being a design somebody lays out and became a fixed
-- structure: the blocks, their order and their measurements are settled, and
-- the only thing that varies is what goes in them. That variable half is this
-- catalogue — the access zones, the services, the venues, the discipline and
-- the accreditation categories of one competition.
--
-- Four kinds of thing, one table, and that is the decision worth explaining.
-- A discipline, a venue, a service and a zone look like four concepts and
-- behave like one: each is a short code, a name, and a line in the glossary
-- printed on the back of every card. They differ only in where they are
-- printed — the discipline opens the first row of boxes, venues finish it,
-- services fill the second row, zones run along the footer band. Four tables
-- would have meant four CRUDs, four policies and four sets of grants for one
-- shape, and a glossary assembled by stitching them back together.
--
-- What a person may enter is reached in two steps, which is how accreditation
-- actually works: the category carries the package — an athlete gets the field
-- of play and the village — and the exceptions are recorded against the
-- person. Without the package, every one of four hundred athletes would be
-- granted zone by zone; without the exceptions, one person needing a single
-- extra door would mean inventing a category for them.
-- =============================================================================

CREATE TYPE accreditation_item_kind AS ENUM ('discipline', 'venue', 'service', 'zone');

-- =============================================================================
-- The catalogue itself
-- =============================================================================

CREATE TABLE accreditation_items (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id         uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    competition_id uuid NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
    kind           accreditation_item_kind NOT NULL,
    code           text        NOT NULL,
    name           text        NOT NULL,
    color_hex      text,
    icon_key       text,
    display_order  smallint    NOT NULL DEFAULT 0,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),

    -- Printed in a box a few millimetres wide. A code that does not fit is not
    -- a validation nicety, it is a card that comes out wrong.
    CONSTRAINT accreditation_items_code_length CHECK (char_length(code) BETWEEN 1 AND 4),
    CONSTRAINT accreditation_items_name_length CHECK (char_length(name) BETWEEN 1 AND 80),

    -- Written straight into a drawing instruction, like the one on
    -- accreditation_categories, so it is checked here too rather than
    -- trusted. Null is the ordinary case for a venue or a service; a zone
    -- is what actually uses this — the footer band and the colour strip
    -- behind a zone's own box are filled with it (PASO's accreditation
    -- card operating system prints Blue/Red/White zones as exactly that:
    -- a colour, not a code).
    CONSTRAINT accreditation_items_color CHECK (color_hex IS NULL OR color_hex ~ '^#[0-9A-Fa-f]{6}$'),

    UNIQUE (competition_id, kind, code),

    -- Redundant against the primary key, and here so the link tables below can
    -- name (competition_id, id) and have the database refuse a category that
    -- grants a zone belonging to another competition.
    UNIQUE (competition_id, id)
);

COMMENT ON TABLE accreditation_items IS
  'Everything a credential can name: the discipline, the venues, the services
   and the access zones of one competition. One table because they are one
   shape — a code, a name, a line in the glossary — and they differ only in
   which row of the card prints them.';

COMMENT ON COLUMN accreditation_items.code IS
  'What is printed in the box, and what the glossary on the back explains. Up
   to four characters, because the box is a few millimetres wide.';

COMMENT ON COLUMN accreditation_items.color_hex IS
  'The colour this entry prints as, as "#rrggbb". Null for most items — a
   venue or a service is printed as its code in the box the layout gives it.
   A zone is the kind that actually carries this: the footer band of both
   faces is filled with the colour of the zones the holder carries, the way
   an Olympic-system card prints "Blue" or "Red" zone access as a colour
   strip rather than as a word.';

COMMENT ON COLUMN accreditation_items.icon_key IS
  'A picture printed instead of the code, in object storage. Null means the
   code is printed as words, which is the usual case — this exists for the
   dining service, drawn as cutlery on every card of this kind.';

CREATE INDEX idx_accreditation_items_competition
    ON accreditation_items(competition_id, kind, display_order);

ALTER TABLE accreditation_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE accreditation_items FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON accreditation_items
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON accreditation_items TO sportfrog_app;

-- Nothing here is public. Which doors a competition's credential opens is the
-- organization's business: public verification (RF-45) answers whether a card
-- is valid, never what it grants (RNF-16).

CREATE TRIGGER trg_accreditation_items_touch BEFORE UPDATE ON accreditation_items
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- =============================================================================
-- Categories
-- =============================================================================

CREATE TABLE accreditation_categories (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id         uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    competition_id uuid NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
    code           text        NOT NULL,
    name           text        NOT NULL,
    color_hex      text        NOT NULL DEFAULT '#1F3864',
    display_order  smallint    NOT NULL DEFAULT 0,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT accreditation_categories_code_length CHECK (char_length(code) BETWEEN 1 AND 4),
    CONSTRAINT accreditation_categories_name_length CHECK (char_length(name) BETWEEN 1 AND 80),

    -- The colour fills the category box on the front and the footer band on
    -- both faces. Checked here rather than trusted, because it is written
    -- straight into a drawing instruction.
    CONSTRAINT accreditation_categories_color CHECK (color_hex ~ '^#[0-9A-Fa-f]{6}$'),

    UNIQUE (competition_id, code),
    UNIQUE (competition_id, id)
);

COMMENT ON TABLE accreditation_categories IS
  'The accreditation category printed on a card — Aa, RTb, E — with the colour
   its box and footer band are filled with.

   Deliberately not the same thing as categories, which is a division of a
   competition by age, weight or gender. They are different axes, and the names
   are close enough to be worth the warning: a person belongs to one of each,
   and only this one reaches the card.';

CREATE INDEX idx_accreditation_categories_competition
    ON accreditation_categories(competition_id, display_order);

ALTER TABLE accreditation_categories ENABLE ROW LEVEL SECURITY;
ALTER TABLE accreditation_categories FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON accreditation_categories
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON accreditation_categories TO sportfrog_app;

CREATE TRIGGER trg_accreditation_categories_touch BEFORE UPDATE ON accreditation_categories
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- =============================================================================
-- What a category grants
-- =============================================================================

CREATE TABLE accreditation_category_items (
    org_id         uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    competition_id uuid NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
    category_id    uuid NOT NULL,
    item_id        uuid NOT NULL,

    PRIMARY KEY (category_id, item_id),

    -- Both ends named with the competition, so a category cannot grant
    -- something belonging to another one. The alternative is a check in the
    -- application — one somebody can forget to write the next time a row is
    -- inserted from somewhere new.
    FOREIGN KEY (competition_id, category_id)
        REFERENCES accreditation_categories(competition_id, id) ON DELETE CASCADE,
    FOREIGN KEY (competition_id, item_id)
        REFERENCES accreditation_items(competition_id, id) ON DELETE CASCADE
);

COMMENT ON TABLE accreditation_category_items IS
  'The package a category carries. Everybody holding it is granted these unless
   an exception recorded against the person says otherwise.';

CREATE INDEX idx_accreditation_category_items_item
    ON accreditation_category_items(item_id);

ALTER TABLE accreditation_category_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE accreditation_category_items FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON accreditation_category_items
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON accreditation_category_items TO sportfrog_app;

-- =============================================================================
-- Who holds which category
-- =============================================================================

CREATE TABLE athlete_accreditations (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id         uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    competition_id uuid NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
    athlete_id     uuid NOT NULL REFERENCES athletes(id) ON DELETE CASCADE,
    category_id    uuid NOT NULL,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),

    -- One per person per competition, and that is the point rather than a
    -- tidiness rule: somebody registered with two teams of the same
    -- competition is one person who gets one card, and without this they would
    -- get two.
    UNIQUE (competition_id, athlete_id),

    UNIQUE (competition_id, id),

    FOREIGN KEY (competition_id, category_id)
        REFERENCES accreditation_categories(competition_id, id) ON DELETE RESTRICT
);

COMMENT ON TABLE athlete_accreditations IS
  'Which accreditation category a person holds in one competition. Restricted
   rather than cascaded on the category: deleting a category people are
   accredited under would silently strip them, so it is refused until they have
   been moved.';

CREATE INDEX idx_athlete_accreditations_category ON athlete_accreditations(category_id);

ALTER TABLE athlete_accreditations ENABLE ROW LEVEL SECURITY;
ALTER TABLE athlete_accreditations FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON athlete_accreditations
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON athlete_accreditations TO sportfrog_app;

CREATE TRIGGER trg_athlete_accreditations_touch BEFORE UPDATE ON athlete_accreditations
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- =============================================================================
-- Exceptions, person by person
-- =============================================================================

CREATE TABLE athlete_accreditation_items (
    org_id           uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    competition_id   uuid NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
    accreditation_id uuid NOT NULL,
    item_id          uuid NOT NULL,

    -- True adds something the category does not carry, false takes away
    -- something it does. Both directions are needed: somebody may be granted a
    -- door their category does not open, and somebody serving a sanction loses
    -- one it does.
    granted          boolean NOT NULL,

    PRIMARY KEY (accreditation_id, item_id),

    FOREIGN KEY (competition_id, accreditation_id)
        REFERENCES athlete_accreditations(competition_id, id) ON DELETE CASCADE,
    FOREIGN KEY (competition_id, item_id)
        REFERENCES accreditation_items(competition_id, id) ON DELETE CASCADE
);

COMMENT ON TABLE athlete_accreditation_items IS
  'What one person has beyond, or short of, what their category carries. Absent
   is the ordinary case: most people get exactly their package.';

CREATE INDEX idx_athlete_accreditation_items_item ON athlete_accreditation_items(item_id);

ALTER TABLE athlete_accreditation_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE athlete_accreditation_items FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON athlete_accreditation_items
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON athlete_accreditation_items TO sportfrog_app;

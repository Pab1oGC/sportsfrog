-- =============================================================================
-- AddAccreditationCatalog — rollback
--
-- Dropped in the reverse order of the Up, so no table is removed while
-- something still references it. The two link tables go first, then what they
-- point at, and the enum last — a type cannot be dropped while a column still
-- holds it.
--
-- Nothing else in the schema references these tables yet: the credential
-- pipeline is wired to the catalogue in a later migration, and keeping this
-- one self-contained is what makes it safe to roll back on its own.
-- =============================================================================

DROP TABLE IF EXISTS athlete_accreditation_items;
DROP TABLE IF EXISTS athlete_accreditations;
DROP TABLE IF EXISTS accreditation_category_items;
DROP TABLE IF EXISTS accreditation_categories;
DROP TABLE IF EXISTS accreditation_items;

DROP TYPE IF EXISTS accreditation_item_kind;

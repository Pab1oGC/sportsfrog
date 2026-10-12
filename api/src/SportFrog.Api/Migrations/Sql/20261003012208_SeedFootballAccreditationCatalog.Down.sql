-- =============================================================================
-- SeedFootballAccreditationCatalog — rollback
--
-- Deletes by the known seeded codes, the same way SeedCatalog's own rollback
-- does for sports: there is no marker distinguishing a seeded row from one an
-- operator added afterwards with the same code, so this is exact only for a
-- database nobody has customized yet. Category links go first, then the
-- categories and items themselves.
-- =============================================================================

DELETE FROM accreditation_category_items
WHERE competition_id IN (SELECT id FROM competitions WHERE sport_code = 'football')
  AND category_id IN (
      SELECT id FROM accreditation_categories
      WHERE competition_id IN (SELECT id FROM competitions WHERE sport_code = 'football')
        AND code IN ('Aa', 'Ao')
  );

DELETE FROM accreditation_categories
WHERE competition_id IN (SELECT id FROM competitions WHERE sport_code = 'football')
  AND code IN ('Aa', 'Ao');

DELETE FROM accreditation_items
WHERE competition_id IN (SELECT id FROM competitions WHERE sport_code = 'football')
  AND (
      (kind = 'discipline' AND code = 'FUT')
      OR (kind = 'venue' AND code IN ('VIL', 'MPC', 'EST'))
      OR (kind = 'service' AND code IN ('TA', 'COM'))
      OR (kind = 'zone' AND code IN ('AZUL', '2', 'R'))
  );

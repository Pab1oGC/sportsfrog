-- =============================================================================
-- AthletePhotosToObjectStorage - rollback
--
-- Puts the column's name and comment back. The values are untouched in both
-- directions: rolling back leaves storage keys in a column called photo_url,
-- which is what the old code will find there and is better than destroying
-- the reference to somebody's photograph to make a name fit.
-- =============================================================================

ALTER TABLE athletes RENAME COLUMN photo_key TO photo_url;

COMMENT ON COLUMN athletes.photo_url IS
  'Image already normalized in orientation, dimensions and format. The
   original uploaded image is not kept.';

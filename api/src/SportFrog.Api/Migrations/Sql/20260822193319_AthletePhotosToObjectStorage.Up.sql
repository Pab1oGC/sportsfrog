-- =============================================================================
-- AthletePhotosToObjectStorage
--
-- The column stops holding the picture and starts holding its address.
--
-- Until now a photograph was written into athletes.photo_url as a data URL:
-- the whole image, base64 encoded, inside a text column. That survives
-- somebody registering a player by hand and does not survive a club uploading
-- four hundred at once — every backup, every replica and every listing that
-- selects the row would carry megabytes that no query ever filters on.
--
-- Renamed rather than added alongside, because the two are the same fact and
-- keeping both would leave the question of which one is the photograph. The
-- name changes with the meaning: what is stored is a key, not a URL, and a
-- column called photo_url holding 'orgs/../athletes/../a1b2.jpg' would be
-- lying to the next person who reads the schema.
--
-- Values already in the column are left exactly as they are. They are real
-- photographs somebody uploaded, and the application recognises a data URL
-- and serves it unchanged; each one converts itself the next time that
-- athlete is corrected. Deleting them here would have been tidier and would
-- have thrown away data to achieve it.
-- =============================================================================

ALTER TABLE athletes RENAME COLUMN photo_url TO photo_key;

COMMENT ON COLUMN athletes.photo_key IS
  'Key of the normalized image in object storage, under the organization''s
   own prefix. The image is turned upright, resized and re-encoded before it
   is stored, which is also what strips the location metadata a phone camera
   records. The original upload is not kept.

   May still hold an inline data URL for photographs uploaded before the
   images moved out of the database. Those are served as they are and are
   converted when the athlete is next corrected.';

-- =============================================================================
-- AddClubContactEmail
--
-- Who to write to when something about a club's fixture changes -- a
-- reprogramming notice needs an address to land on, and today nothing in the
-- schema has one: users have citext email for signing in, but nobody
-- competing does. citext, matching users.email, so two addresses that only
-- differ by case are still the same address.
-- =============================================================================

ALTER TABLE clubs ADD COLUMN contact_email citext;

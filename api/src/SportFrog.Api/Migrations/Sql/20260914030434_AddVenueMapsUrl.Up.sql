-- =============================================================================
-- AddVenueMapsUrl
--
-- Venue.Address is free text -- "how a person would tell someone driving
-- there" -- and nothing computes from it. A Google Maps link is a distinct,
-- structured thing: something a visitor can actually tap to be shown the
-- way, on the public portal as much as in the admin panel. It sits beside
-- Address rather than replacing it, since an organizer may already trust the
-- address alone and never need the link, or vice versa.
-- =============================================================================

ALTER TABLE venues ADD COLUMN maps_url text;

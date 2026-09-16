-- =============================================================================
-- DocumentPortalThemeInSettings - rollback
--
-- Restores the column comment to the exact text InitialSchema set. Nothing
-- else changed, so nothing else is undone.
-- =============================================================================

COMMENT ON COLUMN competitions.settings IS $comment$Expected structure:
{
  "schedule": {
    "slot_minutes": 90,
    "spaces": [
      { "venue_space_id": "uuid", "days": [6,0], "from": "08:00", "to": "14:00" }
    ]
  },
  "public": { "show_standings": true, "show_leaders": true, "show_rosters": false }
}$comment$;

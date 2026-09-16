-- =============================================================================
-- DocumentPortalMomentAndSections
--
-- No column change: competitions.settings is jsonb and this only refreshes
-- its COMMENT, adding the two things the previous refresh
-- (DocumentPortalThemeInSettings) didn't yet know about: the theme's focal
-- point (public.theme.focus_x/focus_y) and the order and naming of the
-- page's own sections (public.section_order). Neither is computed from a
-- match or a standings table -- that part ("what should the cover say right
-- now") is read fresh on every request and never stored at all.
-- =============================================================================

COMMENT ON COLUMN competitions.settings IS $comment$Expected structure (every key optional):
{
  "schedule": {
    "slot_minutes": 90,
    "spaces": [
      { "venue_space_id": "uuid", "days": [6,0], "from": "08:00", "to": "14:00" }
    ]
  },
  "bulletin": {
    "introduction": "text", "sanctions": "text",
    "general_provisions": "text", "contact_info": "text"
  },
  "public": {
    "show_standings": true, "show_leaders": true,
    "show_classification": true, "show_rosters": false,
    "banner_key": "storage-key", "logo_key": "storage-key",
    "accent_color": "#rrggbb",
    "theme": {
      "primary": "#rrggbb", "primary_contrast": "#rrggbb",
      "secondary": "#rrggbb", "surface": "#rrggbb",
      "heading_font": "inter|oswald|bebas-neue|anton|barlow-condensed|archivo-black|teko|montserrat",
      "corners": "sharp|soft|round",
      "hero_style": "solid|gradient|image",
      "focus_x": 50, "focus_y": 50,
      "color_scheme": "auto|light|dark"
    },
    "description": "text",
    "instagram": "url", "facebook": "url", "whats_app": "url", "website": "url",
    "sponsors": [ { "logo_key": "storage-key", "name": "text", "url": "url" } ],
    "section_order": [ { "key": "standings|leaders|classification|calendar", "label": "text" } ]
  }
}$comment$;

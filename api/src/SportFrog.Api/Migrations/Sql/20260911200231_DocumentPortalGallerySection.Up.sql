-- =============================================================================
-- DocumentPortalGallerySection
--
-- No column change: competitions.settings is jsonb. This refreshes its
-- COMMENT with the two things added for the event's own photo gallery --
-- public.show_gallery and public.gallery -- and with "gallery" among the
-- section_order keys, which had stopped listing all of them since
-- DocumentPortalMomentAndSections.
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
    "show_classification": true, "show_rosters": false, "show_gallery": true,
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
    "gallery": [ { "key": "storage-key", "caption": "text" } ],
    "section_order": [ { "key": "standings|leaders|classification|calendar|gallery", "label": "text" } ]
  }
}$comment$;

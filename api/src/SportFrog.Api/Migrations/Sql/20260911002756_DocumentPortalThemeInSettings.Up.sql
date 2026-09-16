-- =============================================================================
-- DocumentPortalThemeInSettings
--
-- No column changes: competitions.settings is jsonb and the application is
-- what gives its contents shape. This only refreshes the column's own
-- COMMENT, which had drifted -- it still described just schedule and three
-- public switches, while the app had since added the bulletin, the cover and
-- logo keys, the accent colour, the description, the socials and the sponsor
-- strip, none of which needed a migration to store.
--
-- Brought fully current here, and extended with "public.theme": the visual
-- system the public page is dressed in. CompetitionConfiguration serializes
-- this column against the structure this comment describes, so the two are
-- meant to be read together. Every key stays optional; an absent one is
-- defaulted when the page is built.
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
      "color_scheme": "auto|light|dark"
    },
    "description": "text",
    "instagram": "url", "facebook": "url", "whats_app": "url", "website": "url",
    "sponsors": [ { "logo_key": "storage-key", "name": "text", "url": "url" } ]
  }
}$comment$;

-- =============================================================================
-- DocumentPortalStyleFields
--
-- No column change: competitions.settings is jsonb and the application is
-- what gives its contents shape. Extends public.theme with three fields
-- added to PortalTheme (SportFrog.Domain/Competitions/CompetitionSettings.cs):
--
--   density      - how much air the whole public page gets, applied as one
--                  spacing multiplier rather than a per-section setting.
--   decoration   - the decorative texture behind the cover's text: one
--                  motif (diagonal lines), two intensities.
--   hero_layout  - how the cover's own text is arranged: the two-column
--                  layout, or everything centered.
--
-- All three are optional and default when absent, same as every other key
-- under public.theme.
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
      "color_scheme": "auto|light|dark",
      "density": "compact|normal|spacious",
      "decoration": "none|subtle|bold",
      "hero_layout": "standard|centered"
    },
    "description": "text",
    "instagram": "url", "facebook": "url", "whats_app": "url", "website": "url",
    "sponsors": [ { "logo_key": "storage-key", "name": "text", "url": "url" } ]
  }
}$comment$;

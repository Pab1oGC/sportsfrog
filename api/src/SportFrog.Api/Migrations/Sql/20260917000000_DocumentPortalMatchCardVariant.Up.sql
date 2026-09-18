-- =============================================================================
-- DocumentPortalMatchCardVariant
--
-- No column change: competitions.settings is jsonb and the application is
-- what gives its contents shape. Extends public.theme with
-- match_card_variant, added to PortalTheme
-- (SportFrog.Domain/Competitions/CompetitionSettings.cs): which layout a
-- match's card renders in, in the calendar (standard row, compact row, or
-- a large featured "matchup" card). Never reaches the knockout bracket,
-- which keeps its own fixed compact card regardless of this field.
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
      "hero_layout": "standard|centered",
      "hero_variant": "standard|scoreboard|editorial|live",
      "standings_variant": "standard|cards|editorial",
      "match_card_variant": "standard|compact|matchup"
    },
    "description": "text",
    "instagram": "url", "facebook": "url", "whats_app": "url", "website": "url",
    "sponsors": [ { "logo_key": "storage-key", "name": "text", "url": "url" } ]
  }
}$comment$;

-- =============================================================================
-- RemoveColorSchemeDocumentPendingFields
--
-- No column change: competitions.settings is jsonb and the application is
-- what gives its contents shape.
--
-- color_scheme dropped: dark mode was removed from the whole application,
-- panel and public portal alike -- one look, light, with no per-visitor or
-- per-competition choice left to document. A row that still carries a
-- stored color_scheme is not migrated (nothing here reads that key any
-- more, so leaving it be costs nothing), just no longer described as part
-- of the shape this column is expected to hold.
--
-- hero_gradient_to, show_logo_background, content_figure and
-- content_figure_color added: fields PortalTheme
-- (SportFrog.Domain/Competitions/CompetitionSettings.cs) picked up across
-- three earlier changes, none of which came with its own copy of this
-- comment.
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
      "hero_gradient_to": "#rrggbb",
      "show_logo_background": true,
      "density": "compact|normal|spacious",
      "decoration": "none|subtle|bold",
      "content_figure": "none|wave|curve-line|shiny-overlay|colored-patterns|contour-line",
      "content_figure_color": "#rrggbb",
      "hero_layout": "standard|centered",
      "hero_variant": "standard|scoreboard|editorial|live",
      "standings_variant": "standard|cards|editorial",
      "match_card_variant": "standard|compact|matchup",
      "bracket_variant": "standard|compact|detailed"
    },
    "description": "text",
    "instagram": "url", "facebook": "url", "whats_app": "url", "website": "url",
    "sponsors": [ { "logo_key": "storage-key", "name": "text", "url": "url" } ]
  }
}$comment$;

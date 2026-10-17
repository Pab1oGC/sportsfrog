/**
 * Fuentes de marca que cualquier página puede necesitar pedir -- un <link> a
 * Google Fonts que React 19 iza al <head> y dedupe solo, sin @fontsource ni
 * build step nuevo. Viven acá, en theme/, y no en un archivo de página, para
 * que pedirlas no obligue a importar desde una página ajena: Fredoka era
 * antes de pages/landing/playful-hero.jsx, y pages/public/public-portal.jsx
 * (que no tiene nada que ver con la landing) la importaba de ahí -- el día
 * que el hero se rediseñó sin Fredoka, esa página habría dejado de compilar.
 *
 * Editorial para titulares formales, UI para wordmarks, mono para números y
 * datos en vivo.
 */

export var FREDOKA_HREF = 'https://fonts.googleapis.com/css2?family=Fredoka:wght@600;700&display=swap';
export var FREDOKA_STACK = '"Fredoka", "Inter", sans-serif';

export var PLEX_SERIF_HREF =
  'https://fonts.googleapis.com/css2?family=IBM+Plex+Serif:ital,wght@0,500;0,600;1,400&display=swap';
export var PLEX_SERIF_STACK = '"IBM Plex Serif", Georgia, serif';

export var INTER_TIGHT_HREF =
  'https://fonts.googleapis.com/css2?family=Inter+Tight:wght@500;600;700&display=swap';
export var INTER_TIGHT_STACK = '"Inter Tight", Inter, sans-serif';

export var JETBRAINS_MONO_HREF =
  'https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@500;600&display=swap';
export var JETBRAINS_MONO_STACK = '"JetBrains Mono", ui-monospace, monospace';

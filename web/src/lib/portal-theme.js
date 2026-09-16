import { createTheme } from '@mui/material/styles';

/* ===========================================================================
   El sistema visual del portal público de una competencia.

   Una sola pieza, sin React, para que la use igual el portal real
   (public-competition.jsx) y la vista previa del estudio (portal-studio.jsx):
   las dos tienen que dibujar lo mismo o la vista previa miente.

   El backend guarda el tema en settings.public.theme y lo entrega ya
   resuelto en portal.theme (ver PortalTheme.Resolve en el dominio). Acá se
   traduce ese objeto a un tema de MUI. Las listas de abajo son las mismas
   que valida PortalTheme.Fonts / CornerStyles / HeroStyles / ColorSchemes:
   si se agrega una opción, va en los dos lados.
   =========================================================================== */

/**
 * Las tipografías de titulares. `inter` es la del cuerpo — no carga nada
 * extra. El resto se trae de Google Fonts con un <link> (React 19 lo iza al
 * <head> y lo deduplica solo); el stack siempre termina en una familia
 * genérica real, así un CDN bloqueado degrada en vez de romper.
 */
export const PORTAL_FONTS = [
  { value: 'inter', label: 'Inter (por defecto)', stack: 'Inter, sans-serif', google: null },
  { value: 'oswald', label: 'Oswald', stack: '"Oswald", "Inter", sans-serif', google: 'Oswald:wght@500;600;700' },
  { value: 'bebas-neue', label: 'Bebas Neue', stack: '"Bebas Neue", "Oswald", sans-serif', google: 'Bebas+Neue' },
  { value: 'anton', label: 'Anton', stack: '"Anton", "Oswald", sans-serif', google: 'Anton' },
  { value: 'barlow-condensed', label: 'Barlow Condensed', stack: '"Barlow Condensed", "Inter", sans-serif', google: 'Barlow+Condensed:wght@500;600;700' },
  { value: 'archivo-black', label: 'Archivo Black', stack: '"Archivo Black", "Inter", sans-serif', google: 'Archivo+Black' },
  { value: 'teko', label: 'Teko', stack: '"Teko", "Oswald", sans-serif', google: 'Teko:wght@500;600;700' },
  { value: 'montserrat', label: 'Montserrat', stack: '"Montserrat", "Inter", sans-serif', google: 'Montserrat:wght@600;700;800' },
];

/** Qué tan cuadrados los bordes. `soft` (12px) es el valor del tema base. */
export const PORTAL_CORNERS = [
  { value: 'sharp', label: 'Rectos', radius: 0 },
  { value: 'soft', label: 'Suaves', radius: 12 },
  { value: 'round', label: 'Redondeados', radius: 22 },
];

/** Cómo se dibuja la portada. */
export const PORTAL_HERO_STYLES = [
  { value: 'solid', label: 'Color plano' },
  { value: 'gradient', label: 'Degradado' },
  { value: 'image', label: 'Imagen de portada' },
];

/** Si la página sigue la preferencia del visitante o queda fija. */
export const PORTAL_COLOR_SCHEMES = [
  { value: 'auto', label: 'Sigue al visitante' },
  { value: 'light', label: 'Siempre claro' },
  { value: 'dark', label: 'Siempre oscuro' },
];

/** El verde de la marca, al que se cae si un tema se abrió sin elegir color. */
const BRAND_GREEN = '#1B8A2E';

/** El estado inicial del formulario del estudio: nada elegido, defaults en las listas. */
export const DEFAULT_PORTAL_THEME = {
  primary: '',
  primaryContrast: '',
  secondary: '',
  surface: '',
  headingFont: 'inter',
  corners: 'soft',
  heroStyle: 'solid',
  focusX: 50,
  focusY: 50,
  colorScheme: 'auto',
};

/* --- Color: parseo y contraste (WCAG 2.1) --------------------------------- */

/** `#rrggbb` (con o sin espacios, con o sin #) → {r,g,b} 0..255, o null. */
export function parseHex(value) {
  if (typeof value !== 'string') return null;
  const match = /^#?([0-9a-fA-F]{6})$/.exec(value.trim());
  if (!match) return null;
  const n = parseInt(match[1], 16);
  return { r: (n >> 16) & 255, g: (n >> 8) & 255, b: n & 255 };
}

/** ¿Es un `#rrggbb` válido? */
export function isHex(value) {
  return parseHex(value) !== null;
}

function toHex(channels) {
  return (
    '#' +
    ['r', 'g', 'b']
      .map((k) => Math.max(0, Math.min(255, Math.round(channels[k]))).toString(16).padStart(2, '0'))
      .join('')
  );
}

function relativeLuminance({ r, g, b }) {
  const channel = (c) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

/**
 * La relación de contraste entre dos colores, 1..21. Null si alguno no es un
 * hex válido. El estudio la usa para avisar cuando el texto sobre el color
 * principal queda por debajo de 4.5:1 (AA para texto normal).
 */
export function contrastRatio(a, b) {
  const pa = parseHex(a);
  const pb = parseHex(b);
  if (!pa || !pb) return null;
  const la = relativeLuminance(pa);
  const lb = relativeLuminance(pb);
  return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
}

/** Negro o blanco, el que más contraste da sobre `bg`. */
export function readableTextOn(bg) {
  const p = parseHex(bg);
  if (!p) return '#ffffff';
  const onWhite = contrastRatio(bg, '#ffffff');
  const onBlack = contrastRatio(bg, '#111315');
  return onBlack >= onWhite ? '#111315' : '#ffffff';
}

function mix(a, b, t) {
  return { r: a.r + (b.r - a.r) * t, g: a.g + (b.g - a.g) * t, b: a.b + (b.b - a.b) * t };
}

/** Aclara un hex hacia el blanco (t 0..1). Igual criterio que theme/index.jsx en oscuro. */
export function lighten(hex, t) {
  const p = parseHex(hex);
  return p ? toHex(mix(p, { r: 255, g: 255, b: 255 }, t)) : hex;
}

/** Oscurece un hex hacia el negro (t 0..1). */
export function darken(hex, t) {
  const p = parseHex(hex);
  return p ? toHex(mix(p, { r: 0, g: 0, b: 0 }, t)) : hex;
}

/* --- Tipografía --------------------------------------------------------------- */

export function portalFontStack(fontValue) {
  const font = PORTAL_FONTS.find((f) => f.value === fontValue);
  return font ? font.stack : PORTAL_FONTS[0].stack;
}

/**
 * La URL de Google Fonts para una tipografía, o null si es la del cuerpo
 * (no hay nada que cargar). Aislada acá a propósito: cambiar a fuentes
 * propias (@fontsource) es tocar solo esta función.
 */
export function portalFontHref(fontValue) {
  const font = PORTAL_FONTS.find((f) => f.value === fontValue);
  if (!font || !font.google) return null;
  return `https://fonts.googleapis.com/css2?family=${font.google}&display=swap`;
}

/* --- Secciones: orden y nombres -------------------------------------------- */

/**
 * Las secciones de la página, en el orden de siempre y con su nombre por
 * defecto. Espejo en JS de `PortalSection.Keys`/`DefaultLabels` del dominio
 * — si se agrega una sección más, va en los dos lados.
 */
export const PORTAL_SECTIONS = [
  { key: 'standings', label: 'Tabla de posiciones' },
  { key: 'leaders', label: 'Líderes' },
  { key: 'classification', label: 'Clasificación' },
  { key: 'calendar', label: 'Calendario' },
  { key: 'gallery', label: 'Fotos' },
];

/**
 * Lo mismo que `PortalSection.Resolve` del backend, para el único lugar del
 * frontend que necesita resolverlo: el estudio de portal lee la lista tal
 * como está guardada (corta, desordenada o ausente) a través de la lectura
 * administrativa de la competencia, que no pasa por el resuelto que sí
 * entrega el portal público. Sin esto, el formulario mostraría solo las
 * secciones que alguna vez se guardaron explícitamente en vez de las cuatro.
 */
export function resolvePortalSections(stored) {
  const knownKeys = new Set(PORTAL_SECTIONS.map((s) => s.key));
  const defaultLabel = {};
  PORTAL_SECTIONS.forEach((s) => { defaultLabel[s.key] = s.label; });

  const resolved = [];
  const seen = new Set();

  (stored || []).forEach((section) => {
    if (!section || !knownKeys.has(section.key) || seen.has(section.key)) return;
    seen.add(section.key);
    const label = section.label && section.label.trim() ? section.label.trim() : defaultLabel[section.key];
    resolved.push({ key: section.key, label });
  });

  PORTAL_SECTIONS.forEach((s) => {
    if (!seen.has(s.key)) resolved.push({ key: s.key, label: s.label });
  });

  return resolved;
}

/* --- Modo claro / oscuro ---------------------------------------------------- */

/**
 * El modo efectivo del portal: si la competencia lo fijó, ese; si no, el del
 * visitante (`visitorMode`, que viene de useColorMode).
 */
export function resolvePortalMode(portal, visitorMode) {
  const scheme = portal?.theme?.colorScheme || 'auto';
  if (scheme === 'light' || scheme === 'dark') return scheme;
  return visitorMode === 'dark' ? 'dark' : 'light';
}

/* --- Portada --------------------------------------------------------------- */

/**
 * El fondo de la portada, calculado igual para el portal y la vista previa.
 * `bannerUrl` es el enlace ya firmado (portal.bannerUrl) o una data URL en el
 * estudio. `focusX`/`focusY` (0..100, default 50 = centrado) solo importan
 * con imagen: dicen qué parte de la foto no se pierde cuando el ancho de la
 * pantalla la recorta. Devuelve props sx: { backgroundColor, backgroundImage,
 * backgroundPosition }.
 */
export function heroBackground({ heroStyle, primary, bannerUrl, focusX, focusY }) {
  const color = isHex(primary) ? primary : undefined;
  const wantsImage = heroStyle === 'image' && bannerUrl;
  const darkOverlay = 'linear-gradient(180deg, rgba(0,0,0,0.35), rgba(0,0,0,0.6))';
  const position = `${clampAxis(focusX)}% ${clampAxis(focusY)}%`;

  if (wantsImage) {
    return { backgroundColor: color, backgroundImage: `${darkOverlay}, url(${bannerUrl})`, backgroundPosition: position };
  }
  if (heroStyle === 'gradient' && color) {
    return { backgroundColor: color, backgroundImage: `linear-gradient(135deg, ${color}, ${darken(color, 0.32)})`, backgroundPosition: undefined };
  }
  // solid, o image sin banner: cae en color plano.
  return { backgroundColor: color, backgroundImage: undefined, backgroundPosition: undefined };
}

function clampAxis(value) {
  const n = Number(value);
  return Number.isFinite(n) ? Math.max(0, Math.min(100, n)) : 50;
}

/* --- El tema de MUI ------------------------------------------------------- */

const DARK_SURFACE = '#111315';
const DARK_PAPER = '#1A1D20';
const LIGHT_SURFACE = '#f4f6f8';
const LIGHT_PAPER = '#ffffff';

/**
 * Construye el tema del portal a partir del `portal` que entrega la API y el
 * modo del visitante. Si la competencia no personalizó nada (ni tema ni el
 * viejo accentColor) devuelve `base` intacto: el portal se ve exactamente
 * como antes de que esto existiera.
 *
 * @param {object} args
 * @param {import('@mui/material/styles').Theme} args.base - el tema de la app
 * @param {object|null|undefined} args.portal - comp.portal de la API
 * @param {'light'|'dark'} args.visitorMode - useColorMode().mode
 */
export function buildPortalTheme({ base, portal, visitorMode }) {
  const theme = portal?.theme || null;
  const legacyAccent = portal?.accentColor || null;
  const primarySource = theme?.primary || legacyAccent;

  // Nada personalizado: el portal queda igual que siempre. Esta es la
  // garantía de que activar la función no cambia una sola competencia que no
  // la usó.
  if (!theme && !primarySource) return base;

  const mode = resolvePortalMode(portal, visitorMode);
  const dark = mode === 'dark';

  const primary = isHex(primarySource) ? primarySource.trim() : base.palette.primary.main;
  // En oscuro se aclara la marca para que pase el contraste sobre un fondo
  // casi negro — el mismo ajuste que hace theme/index.jsx con su verde.
  const primaryMain = dark ? lighten(primary, 0.26) : primary;

  const secondarySource = isHex(theme?.secondary) ? theme.secondary.trim() : primary;
  const secondaryMain = dark ? lighten(secondarySource, 0.26) : secondarySource;

  const contrastText = isHex(theme?.primaryContrast)
    ? theme.primaryContrast.trim()
    : readableTextOn(primaryMain);

  const cornerRadius = (PORTAL_CORNERS.find((c) => c.value === (theme?.corners || 'soft')) || PORTAL_CORNERS[1]).radius;
  const headingFontValue = theme?.headingFont || 'inter';
  const headingStack = portalFontStack(headingFontValue);

  const surface = isHex(theme?.surface)
    ? theme.surface.trim()
    : dark ? DARK_SURFACE : LIGHT_SURFACE;
  const paper = dark ? DARK_PAPER : LIGHT_PAPER;

  // Los titulares. Solo se tocan si se eligió una fuente distinta a la del
  // cuerpo — así una competencia sin tipografía propia conserva la jerarquía
  // exacta del tema base.
  const headingVariants =
    headingFontValue === 'inter'
      ? {}
      : ['h1', 'h2', 'h3', 'h4', 'h5', 'h6'].reduce((acc, key) => {
          acc[key] = { fontFamily: headingStack };
          return acc;
        }, {});

  // Cuando la competencia fija un modo distinto al del visitante, mezclar
  // { mode } sobre `base` no alcanza: createPalette no pisa el texto y los
  // divisores que ya venían del otro modo. Se pasan completos.
  const surfaces = dark
    ? {
        text: { primary: '#F4F6F8', secondary: 'rgba(244,246,248,0.7)', disabled: 'rgba(244,246,248,0.4)' },
        divider: 'rgba(255,255,255,0.12)',
        background: { default: surface, paper },
        action: {
          active: 'rgba(255,255,255,0.7)',
          hover: 'rgba(255,255,255,0.08)',
          selected: 'rgba(255,255,255,0.16)',
          disabled: 'rgba(255,255,255,0.3)',
          disabledBackground: 'rgba(255,255,255,0.12)',
        },
      }
    : {
        text: { primary: 'rgba(0,0,0,0.87)', secondary: 'rgba(0,0,0,0.6)', disabled: 'rgba(0,0,0,0.38)' },
        divider: 'rgba(0,0,0,0.12)',
        background: { default: surface, paper },
      };

  // El tema base (web/src/theme/index.jsx) hornea en MuiCard y MuiDataGrid
  // colores que dependen del modo, decididos cuando se creó — el modo del
  // panel. Si el portal fija el modo contrario, esos overrides quedan al
  // revés (una grilla clara con texto claro encima). Se vuelven a declarar
  // acá para el modo ya resuelto.
  const componentOverrides = {
    MuiCard: {
      styleOverrides: {
        root: {
          boxShadow: dark ? 'none' : '0 2px 12px rgba(0,0,0,0.08)',
          border: dark ? '1px solid rgba(255,255,255,0.08)' : undefined,
        },
      },
    },
    MuiDataGrid: {
      styleOverrides: {
        root: {
          border: dark ? '1px solid rgba(255,255,255,0.1)' : '1px solid rgba(0,0,0,0.08)',
          backgroundColor: paper,
          boxShadow: dark ? 'none' : '0 2px 12px rgba(0,0,0,0.06)',
        },
        columnHeaders: {
          backgroundColor: dark ? 'rgba(255,255,255,0.03)' : 'rgba(0,0,0,0.02)',
        },
      },
    },
  };

  // light/dark de primary/secondary van explícitos: el merge sobre `base`
  // conserva los tonos que MUI ya calculó para el verde, y el hover de un
  // botón (primary.dark) saldría verde sobre un primary azul. Mismo
  // tonalOffset 0.2 que usa MUI por defecto. Se mezcla sobre `base` para
  // heredar su tipografía, sus breakpoints y sus overrides de componentes;
  // `components` es un tercer argumento para que createTheme lo funda slot a
  // slot con los del panel.
  return createTheme(
    base,
    {
      palette: {
        mode,
        primary: {
          main: primaryMain,
          light: lighten(primaryMain, 0.2),
          dark: darken(primaryMain, 0.2),
          contrastText,
        },
        secondary: {
          main: secondaryMain,
          light: lighten(secondaryMain, 0.2),
          dark: darken(secondaryMain, 0.2),
          contrastText: readableTextOn(secondaryMain),
        },
        ...surfaces,
      },
      shape: { borderRadius: cornerRadius },
      typography: { ...headingVariants },
    },
    { components: componentOverrides },
  );
}

import { createTheme } from '@mui/material/styles';

/* ===========================================================================
   El sistema visual del portal público de una competencia.

   Una sola pieza, sin React, para que la use igual el portal real
   (public-competition.jsx) y la vista previa del estudio (portal-studio.jsx):
   las dos tienen que dibujar lo mismo o la vista previa miente.

   El backend guarda el tema en settings.public.theme y lo entrega ya
   resuelto en portal.theme (ver PortalTheme.Resolve en el dominio). Acá se
   traduce ese objeto a un tema de MUI. Las listas de abajo son las mismas
   que valida PortalTheme.Fonts / CornerStyles / HeroStyles: si se agrega
   una opción, va en los dos lados.

   Un solo modo, claro, en todo el portal -- sin interruptor y sin seguir la
   preferencia del sistema del visitante.
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

/**
 * Cuánto aire tiene la página entera -- un solo multiplicador de espaciado
 * (ver `spacingUnit` en `buildPortalTheme`), no un ajuste sección por
 * sección. `normal` es 8, el mismo valor por defecto de MUI y el que ya usa
 * el tema base (`theme/index.jsx`), así que "normal" es pixel-idéntico a
 * como se veía el portal antes de que esto existiera.
 */
export const PORTAL_DENSITIES = [
  { value: 'compact', label: 'Compacta', unit: 6 },
  { value: 'normal', label: 'Normal', unit: 8 },
  { value: 'spacious', label: 'Espaciosa', unit: 11 },
];

/**
 * La textura detrás del texto de la portada. Un solo motivo (líneas
 * diagonales), dos intensidades -- no una librería de patrones para elegir.
 */
export const PORTAL_DECORATIONS = [
  { value: 'none', label: 'Ninguna' },
  { value: 'subtle', label: 'Sutil' },
  { value: 'bold', label: 'Marcada' },
];

/**
 * Una figura de fondo detrás del contenido (tabla, calendario, fotos...) --
 * nunca la portada, que ya tiene la suya (PORTAL_DECORATIONS). Cada valor
 * nombra un archivo bajo /portal/ (ver content-figure.jsx), no un estilo
 * aplicado a todos -- si se agrega uno nuevo, va en los dos lados: acá y en
 * `PortalTheme.ContentFigures` del backend.
 */
export const PORTAL_CONTENT_FIGURES = [
  { value: 'none', label: 'Ninguna' },
  { value: 'wave', label: 'Olas' },
  { value: 'curve-line', label: 'Líneas curvas' },
  { value: 'shiny-overlay', label: 'Brillo diagonal' },
  { value: 'colored-patterns', label: 'Patrones' },
  { value: 'contour-line', label: 'Líneas de contorno' },
];

/** Cómo se ordena el texto de la portada. */
export const PORTAL_HERO_LAYOUTS = [
  { value: 'standard', label: 'Estándar (dos columnas)' },
  { value: 'centered', label: 'Centrada' },
];

/**
 * Qué composición de portada se dibuja -- no un restyling de la misma, un
 * componente distinto (ver web/src/pages/public/hero/). `heroLayout` solo
 * aplica a `standard`; las otras tres fijan su propia disposición.
 */
export const PORTAL_HERO_VARIANTS = [
  { value: 'standard', label: 'Estándar' },
  { value: 'scoreboard', label: 'Marcador' },
  { value: 'editorial', label: 'Editorial' },
  { value: 'live', label: 'En vivo' },
];

/** Qué disposición usa la tabla de posiciones -- ver web/src/pages/public/standings/. */
export const PORTAL_STANDINGS_VARIANTS = [
  { value: 'standard', label: 'Tabla' },
  { value: 'cards', label: 'Tarjetas' },
  { value: 'editorial', label: 'Ranking' },
];

/** Qué disposición usa la tarjeta de partido en el calendario -- ver web/src/pages/public/match-card/. */
export const PORTAL_MATCH_CARD_VARIANTS = [
  { value: 'standard', label: 'Estándar' },
  { value: 'compact', label: 'Compacta' },
  { value: 'matchup', label: 'Destacada' },
];

/** Qué disposición usa el cruce de la llave de eliminatoria -- ver web/src/pages/public/bracket/. */
export const PORTAL_BRACKET_VARIANTS = [
  { value: 'standard', label: 'Estándar' },
  { value: 'compact', label: 'Compacta' },
  { value: 'detailed', label: 'Detallada' },
];

/** El fucsia de la marca, al que se cae si un tema se abrió sin elegir color. */
const BRAND_FUCSIA = '#F50057';

/** El estado inicial del formulario del estudio: nada elegido, defaults en las listas. */
export const DEFAULT_PORTAL_THEME = {
  primary: '',
  primaryContrast: '',
  secondary: '',
  surface: '',
  headingFont: 'inter',
  corners: 'soft',
  heroStyle: 'solid',
  heroGradientTo: '',
  focusX: 50,
  focusY: 50,
  density: 'normal',
  decoration: 'none',
  contentFigure: 'none',
  contentFigureColor: '',
  showLogoBackground: true,
  heroLayout: 'standard',
  heroVariant: 'standard',
  standingsVariant: 'standard',
  matchCardVariant: 'standard',
  bracketVariant: 'standard',
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
  { key: 'bracket', label: 'Llave' },
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

/* --- Portada --------------------------------------------------------------- */

/**
 * El fondo de la portada, calculado igual para el portal y la vista previa.
 * `bannerUrl` es el enlace ya firmado (portal.bannerUrl) o una data URL en el
 * estudio. `focusX`/`focusY` (0..100, default 50 = centrado) solo importan
 * con imagen: dicen qué parte de la foto no se pierde cuando el ancho de la
 * pantalla la recorta. `gradientTo` solo importa con degradado: el segundo
 * color elegido a mano, o -si no se eligió uno- el mismo principal oscurecido
 * que este degradado dibujaba antes de que ese campo existiera (una
 * competencia vieja con "gradient" guardado no cambia de look). Devuelve
 * props sx: { backgroundColor, backgroundImage, backgroundPosition }.
 */
export function heroBackground({ heroStyle, primary, gradientTo, bannerUrl, focusX, focusY }) {
  const color = isHex(primary) ? primary : undefined;
  const wantsImage = heroStyle === 'image' && bannerUrl;
  const darkOverlay = 'linear-gradient(180deg, rgba(0,0,0,0.35), rgba(0,0,0,0.6))';
  const position = `${clampAxis(focusX)}% ${clampAxis(focusY)}%`;

  if (wantsImage) {
    return { backgroundColor: color, backgroundImage: `${darkOverlay}, url(${bannerUrl})`, backgroundPosition: position };
  }
  if (heroStyle === 'gradient' && color) {
    const to = isHex(gradientTo) ? gradientTo : darken(color, 0.32);
    return { backgroundColor: color, backgroundImage: `linear-gradient(135deg, ${color}, ${to})`, backgroundPosition: undefined };
  }
  // solid, o image sin banner: cae en color plano.
  return { backgroundColor: color, backgroundImage: undefined, backgroundPosition: undefined };
}

function clampAxis(value) {
  const n = Number(value);
  return Number.isFinite(n) ? Math.max(0, Math.min(100, n)) : 50;
}

/* --- El tema de MUI ------------------------------------------------------- */

/**
 * Reconstruye `theme.spacing` para la densidad elegida. No se puede pasar
 * `{ spacing: N }` a `createTheme(base, {...})`: `base` ya es un tema
 * resuelto, así que `base.spacing` llega como función, no como número, y el
 * merge final de createTheme no la reconstruye -- reemplazaría la función
 * por el número crudo y cualquier `sx={{ p: 2 }}` del portal rompería
 * (`theme.spacing is not a function`). Hay que asignar la función ya hecha.
 *
 * Mismo algoritmo que `createSpacing`/`createUnaryUnit` de `@mui/system`
 * para una unidad numérica (verificado contra su código fuente): cada
 * argumento se multiplica por la unidad y se formatea en `px`; sin
 * argumentos equivale a factor 1; varios argumentos se unen con un espacio,
 * igual que soporta `theme.spacing(1, 2)`. No se importa el paquete en sí
 * -- no es una dependencia declarada de este proyecto (solo llega transitivo
 * a través de @mui/material) y agregarla llevó a un symlink de pnpm roto en
 * este entorno; el llamado real que hace este portal es siempre un único
 * número positivo, así que reimplementar esa porción puntual es más simple
 * y no depende de un paquete que no se puede resolver.
 */
function createPortalSpacing(unit) {
  function spacing(...factors) {
    const args = factors.length === 0 ? [1] : factors;
    return args.map((factor) => (typeof factor === 'string' ? factor : `${unit * factor}px`)).join(' ');
  }
  spacing.mui = true;
  return spacing;
}

const DEFAULT_SURFACE = '#f4f6f8';
const DEFAULT_PAPER = '#ffffff';

/**
 * Construye el tema del portal a partir del `portal` que entrega la API. Si
 * la competencia no personalizó nada (ni tema ni el viejo accentColor)
 * devuelve `base` intacto: el portal se ve exactamente como antes de que esto
 * existiera.
 *
 * @param {object} args
 * @param {import('@mui/material/styles').Theme} args.base - el tema de la app
 * @param {object|null|undefined} args.portal - comp.portal de la API
 */
export function buildPortalTheme({ base, portal }) {
  const theme = portal?.theme || null;
  const legacyAccent = portal?.accentColor || null;
  const primarySource = theme?.primary || legacyAccent;

  // Nada personalizado: el portal queda igual que siempre. Esta es la
  // garantía de que activar la función no cambia una sola competencia que no
  // la usó.
  if (!theme && !primarySource) return base;

  const primaryMain = isHex(primarySource) ? primarySource.trim() : base.palette.primary.main;
  const secondaryMain = isHex(theme?.secondary) ? theme.secondary.trim() : primaryMain;

  const contrastText = isHex(theme?.primaryContrast)
    ? theme.primaryContrast.trim()
    : readableTextOn(primaryMain);

  const cornerRadius = (PORTAL_CORNERS.find((c) => c.value === (theme?.corners || 'soft')) || PORTAL_CORNERS[1]).radius;
  const headingFontValue = theme?.headingFont || 'inter';
  const headingStack = portalFontStack(headingFontValue);

  const surface = isHex(theme?.surface) ? theme.surface.trim() : DEFAULT_SURFACE;
  const paper = DEFAULT_PAPER;

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

  const componentOverrides = {
    MuiCard: {
      styleOverrides: {
        root: { boxShadow: '0 2px 12px rgba(0,0,0,0.08)' },
      },
    },
    MuiDataGrid: {
      styleOverrides: {
        root: {
          border: '1px solid rgba(0,0,0,0.08)',
          backgroundColor: paper,
          boxShadow: '0 2px 12px rgba(0,0,0,0.06)',
        },
        columnHeaders: { backgroundColor: 'rgba(0,0,0,0.02)' },
      },
    },
  };

  // primary/secondary van explícitos: el merge sobre `base` conserva los
  // tonos que MUI ya calculó para el verde, y el hover de un botón
  // (primary.dark) saldría verde sobre un primary azul. Mismo tonalOffset
  // 0.2 que usa MUI por defecto. Se mezcla sobre `base` para heredar su
  // tipografía, sus breakpoints y sus overrides de componentes; `components`
  // es un tercer argumento para que createTheme lo funda slot a slot con los
  // del panel.
  const result = createTheme(
    base,
    {
      palette: {
        mode: 'light',
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
        text: { primary: 'rgba(0,0,0,0.87)', secondary: 'rgba(0,0,0,0.6)', disabled: 'rgba(0,0,0,0.38)' },
        divider: 'rgba(0,0,0,0.12)',
        background: { default: surface, paper },
      },
      shape: { borderRadius: cornerRadius },
      typography: { ...headingVariants },
    },
    { components: componentOverrides },
  );

  // Ver createPortalSpacing más arriba para el porqué de reasignar esto
  // después de crear el tema en vez de pasarlo como opción.
  const density = PORTAL_DENSITIES.find((d) => d.value === (theme?.density || 'normal')) || PORTAL_DENSITIES[1];
  result.spacing = createPortalSpacing(density.unit);

  return result;
}

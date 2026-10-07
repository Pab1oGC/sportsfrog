import { darken, lighten, parseHex, readableTextOn } from 'src/lib/portal-theme';

const FALLBACK_COLORS = ['#1769AA', '#F4B400'];

function channelHex(value) {
  return Math.max(0, Math.min(255, Math.round(value))).toString(16).padStart(2, '0');
}

function rgbToHex({ r, g, b }) {
  return `#${channelHex(r)}${channelHex(g)}${channelHex(b)}`.toUpperCase();
}

function rgbDistance(a, b) {
  return Math.sqrt(((a.r - b.r) ** 2) + ((a.g - b.g) ** 2) + ((a.b - b.b) ** 2));
}

function saturation({ r, g, b }) {
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  return max === 0 ? 0 : (max - min) / max;
}

function perceivedLightness({ r, g, b }) {
  return (0.299 * r + 0.587 * g + 0.114 * b) / 255;
}

function complement(hex) {
  const rgb = parseHex(hex);
  return rgb ? rgbToHex({ r: 255 - rgb.r, g: 255 - rgb.g, b: 255 - rgb.b }) : FALLBACK_COLORS[1];
}

function normalizedColors(colors) {
  const unique = [];
  (colors || []).forEach((color) => {
    const parsed = typeof color === 'string' ? parseHex(color) : color;
    if (!parsed) return;
    const hex = rgbToHex(parsed);
    if (!unique.includes(hex)) unique.push(hex);
  });
  return unique.length ? unique : FALLBACK_COLORS;
}

function mostDifferent(base, colors) {
  const parsedBase = parseHex(base);
  const alternatives = colors.filter((color) => color !== base);
  // Blanco, gris o negro suelen ser soporte/fondo del escudo, no su color
  // secundario. Si existe otra tinta real, se prefiere aunque esté un poco
  // más cerca del principal en distancia RGB.
  const chromatic = alternatives.filter((color) => saturation(parseHex(color)) >= 0.18);
  return (chromatic.length ? chromatic : alternatives)
    .map((color) => ({ color, distance: rgbDistance(parsedBase, parseHex(color)) }))
    .sort((a, b) => b.distance - a.distance)[0]?.color || complement(base);
}

function theme(primary, secondary, extras) {
  return {
    primary,
    primaryContrast: readableTextOn(primary),
    secondary,
    surface: lighten(primary, 0.96).toUpperCase(),
    heroGradientTo: darken(primary, 0.38).toUpperCase(),
    contentFigureColor: primary,
    showLogoBackground: true,
    ...extras,
  };
}

/**
 * Convierte los colores dominantes de un logo en tres direcciones visuales
 * completas. Es pura a propósito: además de ser testeable, el resultado no
 * depende del navegador que haya extraído los píxeles.
 */
export function buildThemeRecommendations(colors) {
  const palette = normalizedColors(colors);
  const primary = palette[0];
  const secondary = mostDifferent(primary, palette);
  const primaryRgb = parseHex(primary);
  const vividPrimary = saturation(primaryRgb) < 0.18 ? darken(primary, 0.18).toUpperCase() : primary;
  const premiumPrimary = perceivedLightness(primaryRgb) > 0.5
    ? darken(primary, 0.58).toUpperCase()
    : darken(primary, 0.28).toUpperCase();

  return [
    {
      id: 'identity',
      name: 'Identidad fiel',
      description: 'Respeta los colores del escudo con una presencia limpia y versátil.',
      theme: theme(vividPrimary, secondary, {
        headingFont: 'montserrat', corners: 'soft', heroStyle: 'gradient', density: 'normal',
        decoration: 'subtle', contentFigure: 'contour-line', heroLayout: 'standard',
        heroVariant: 'standard', standingsVariant: 'standard', matchCardVariant: 'standard', bracketVariant: 'standard',
      }),
    },
    {
      id: 'sport',
      name: 'Impacto deportivo',
      description: 'Más contraste, titulares fuertes y componentes pensados para competencia.',
      theme: theme(darken(vividPrimary, 0.18).toUpperCase(), secondary, {
        headingFont: 'barlow-condensed', corners: 'sharp', heroStyle: 'gradient', density: 'compact',
        decoration: 'bold', contentFigure: 'colored-patterns', heroLayout: 'centered',
        heroVariant: 'scoreboard', standingsVariant: 'cards', matchCardVariant: 'matchup', bracketVariant: 'detailed',
      }),
    },
    {
      id: 'premium',
      name: 'Editorial premium',
      description: 'Una base sobria que deja al logo y a los resultados ocupar el centro.',
      theme: theme(premiumPrimary, lighten(secondary, 0.12).toUpperCase(), {
        headingFont: 'archivo-black', corners: 'round', heroStyle: 'gradient', density: 'spacious',
        decoration: 'subtle', contentFigure: 'curve-line', heroLayout: 'centered',
        heroVariant: 'editorial', standingsVariant: 'editorial', matchCardVariant: 'compact', bracketVariant: 'compact',
      }),
    },
  ];
}

function loadImage(source) {
  return new Promise((resolve, reject) => {
    const image = new Image();
    if (!source.startsWith('data:') && !source.startsWith('blob:')) image.crossOrigin = 'anonymous';
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('No se pudo leer la imagen.'));
    image.src = source;
  });
}

/** Extrae hasta cuatro colores dominantes, ignorando transparencia y fondos casi blancos. */
export async function extractLogoColors(source) {
  const image = await loadImage(source);
  const size = 96;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext('2d', { willReadFrequently: true });
  if (!context) throw new Error('Tu navegador no permite analizar esta imagen.');

  const scale = Math.min(size / image.naturalWidth, size / image.naturalHeight);
  const width = Math.max(1, Math.round(image.naturalWidth * scale));
  const height = Math.max(1, Math.round(image.naturalHeight * scale));
  context.clearRect(0, 0, size, size);
  context.drawImage(image, (size - width) / 2, (size - height) / 2, width, height);

  let pixels;
  try {
    pixels = context.getImageData(0, 0, size, size).data;
  } catch {
    throw new Error('No se pudo analizar el logo por restricciones de la imagen. Volvé a subir el archivo.');
  }

  const buckets = new Map();
  for (let i = 0; i < pixels.length; i += 16) {
    const alpha = pixels[i + 3];
    if (alpha < 150) continue;
    const raw = { r: pixels[i], g: pixels[i + 1], b: pixels[i + 2] };
    const light = perceivedLightness(raw);
    if (light > 0.96) continue;
    const rgb = {
      r: Math.min(255, Math.round(raw.r / 24) * 24),
      g: Math.min(255, Math.round(raw.g / 24) * 24),
      b: Math.min(255, Math.round(raw.b / 24) * 24),
    };
    const key = `${rgb.r},${rgb.g},${rgb.b}`;
    const chroma = saturation(raw);
    const score = 0.35 + chroma + (light > 0.08 && light < 0.9 ? 0.25 : 0);
    const previous = buckets.get(key) || { ...rgb, score: 0 };
    previous.score += score;
    buckets.set(key, previous);
  }

  const ranked = [...buckets.values()].sort((a, b) => b.score - a.score);
  const selected = [];
  ranked.forEach((candidate) => {
    if (selected.length >= 4) return;
    if (selected.every((picked) => rgbDistance(candidate, picked) >= 74)) selected.push(candidate);
  });
  if (!selected.length) throw new Error('No encontramos suficiente color en el logo. Probá con una versión de mayor calidad.');
  return selected.map(rgbToHex);
}

export async function recommendThemesFromLogo(source) {
  return buildThemeRecommendations(await extractLogoColors(source));
}

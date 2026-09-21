import { alpha } from '@mui/material/styles';

/**
 * ÚNICO lugar de la app donde viven los colores de la marca. Para cambiar la
 * paleta se edita este archivo y nada más: el tema (theme/index.jsx), los
 * overrides (theme/components.js) y las páginas leen de acá a través de los
 * tokens del tema (`primary.main`, `brand.edge`, `overlay.scrim`...), nunca
 * un hex escrito a mano.
 *
 * Dos capas, cada una con una sola razón para cambiar:
 *
 *   1. `swatches`        -- QUÉ colores existen. Cambia si la marca cambia
 *                           de colores.
 *   2. `createAppPalette` -- QUÉ PAPEL cumple cada color (principal, borde,
 *                           fondo de página...). Cambia si se quiere
 *                           reasignar un rol sin tocar los colores.
 *
 * Este archivo es solo datos: no importa componentes ni conoce páginas.
 * Tampoco toca el portal público, que arma su propio tema en
 * lib/portal-theme.js a partir de lo que el organizador elige en el estudio.
 */

// Capa 1 -- los colores. Los azules son los de la portada (landing-page.jsx y
// playful-hero.jsx); el verde lima es el acento que ya usa la insignia del hero.
export const swatches = {
  blueDeep: '#1D709F',   // azul principal
  blueLight: '#4CB8E6',  // azul claro: bordes y contornos
  blueBright: '#00A4D1', // cian: hover y brillos
  blueTint: '#E7F5FD',   // tinte de superficies (franjas, hero)
  bluePage: '#F5FBFE',   // fondo de página
  lime: '#7CFC00',       // acento
  ink: '#111315',        // texto y sombras
  white: '#ffffff',
};

// Podio (oro / plata / bronce). Aparte de `swatches` porque no es color de
// marca sino de significado, y porque el portal público también los usa
// (medal-circle.jsx los re-exporta como MEDAL_COLORS).
export const medalColors = { 1: '#C9A227', 2: '#8E8E93', 3: '#B87333' };

// Capa 2 -- los roles. Devuelve la `palette` que recibe createTheme, incluidas
// tres claves propias del proyecto:
//   brand   -- colores de marca que MUI no trae (borde, brillo, tinte, acento)
//   overlay -- capas semitransparentes (el fondo que tapa la pantalla tras un modal)
//   ui      -- colores de piezas puntuales que theme/components.js necesita
//              (encabezado de las tablas, barra de scroll)
export function createAppPalette(colors = swatches) {
  return {
    mode: 'light',
    primary: { main: colors.blueDeep, light: colors.blueLight, contrastText: colors.white },
    secondary: { main: colors.blueBright, contrastText: colors.white },
    background: { default: colors.bluePage, paper: colors.white },
    text: {
      primary: colors.ink,
      secondary: alpha(colors.ink, 0.65),
      disabled: alpha(colors.ink, 0.38),
    },
    divider: alpha(colors.ink, 0.12),
    action: { hover: alpha(colors.blueDeep, 0.08) },
    brand: {
      edge: colors.blueLight,
      bright: colors.blueBright,
      tint: colors.blueTint,
      page: colors.bluePage,
      accent: colors.lime,
    },
    overlay: { scrim: alpha(colors.ink, 0.5) },
    ui: {
      tableHeader: alpha(colors.blueDeep, 0.05),
      scrollbarThumb: colors.blueLight,
    },
  };
}

import { alpha } from '@mui/material/styles';

/**
 * Sombras que resuelven contra `theme.palette` en vez de un hex propio -- son
 * funciones de `theme` para que `sx` las llame con la paleta vigente
 * (theme/palette.js), así que cambiar la marca ahí les cambia el color a
 * estas sin tocarlas. `stickerShadow` es del registro de marca (la usa
 * public-portal.jsx); `softShadow` es del registro formal de la landing.
 */

// Sombra "sticker": un bloque sólido del color principal, sin desenfoque.
// Sigue en pie porque public-portal.jsx la usa -- la landing la dejó atrás
// con su registro formal (ver softShadow más abajo).
export function stickerShadow(offset, opacity) {
  return function(theme) { return '0 ' + offset + 'px 0 ' + alpha(theme.palette.primary.main, opacity); };
}

// Sombra neutra de tres capas, sin color de marca -- la elevación de un panel
// claro cualquiera, en vez del bloque sólido de stickerShadow. Para el
// registro formal de la landing (marcos de producto, paneles de sección);
// `intensity` escala las tres capas juntas, de una sola perilla.
export function softShadow(intensity = 1) {
  return function(theme) {
    var ink = theme.palette.text.primary;
    return [
      '0 ' + (1 * intensity) + 'px ' + (2 * intensity) + 'px ' + alpha(ink, 0.04),
      '0 ' + (8 * intensity) + 'px ' + (24 * intensity) + 'px ' + alpha(ink, 0.08),
      '0 ' + (24 * intensity) + 'px ' + (64 * intensity) + 'px ' + alpha(ink, 0.06),
    ].join(', ');
  };
}

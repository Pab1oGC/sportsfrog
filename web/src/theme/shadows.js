import { alpha } from '@mui/material/styles';

/**
 * Sombras compuestas con el color principal del tema. Son funciones de `theme`
 * para que `sx` las resuelva con la paleta vigente (theme/palette.js): así la
 * portada (landing-page.jsx) y el directorio (public-portal.jsx) comparten el
 * mismo estilo sin repetir un solo hex.
 */

// Sombra "sticker": un bloque sólido del color principal, sin desenfoque.
export function stickerShadow(offset, opacity) {
  return function(theme) { return '0 ' + offset + 'px 0 ' + alpha(theme.palette.primary.main, opacity); };
}

// Sombra difusa del color principal, para los botones y la tarjeta final.
export function glowShadow(y, blur, opacity) {
  return function(theme) { return '0 ' + y + 'px ' + blur + 'px ' + alpha(theme.palette.primary.main, opacity); };
}

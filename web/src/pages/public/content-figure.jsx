import Box from '@mui/material/Box';

/* ===========================================================================
   La figura de fondo detrás del contenido del portal (tabla, calendario,
   fotos...) -- nunca la portada, que tiene su propia decoración
   (HeroDecoration, portal-hero.jsx). Los archivos viven en web/public/portal/
   como arte ya armado (varios colores, opacidades y trazos propios): en vez
   de reescribir cada uno a mano para que use un solo color -- frágil, y hay
   que repetirlo por cada figura que se agregue -- se usan como máscara CSS:
   el navegador lee el alfa de cada píxel del SVG y pinta solo con el color
   elegido detrás de esa máscara. Cualquier arte que se agregue a esa carpeta
   funciona igual, sin tocar este archivo salvo por la entrada en el mapa de
   abajo (y en PORTAL_CONTENT_FIGURES/PortalTheme.ContentFigures, para que se
   pueda elegir).
   =========================================================================== */

const CONTENT_FIGURE_FILES = {
  wave: 'Wave.svg',
  'curve-line': 'Curve Line.svg',
  'shiny-overlay': 'Shiny Overlay.svg',
  'colored-patterns': 'Colored Patterns.svg',
  'contour-line': 'Contour Line.svg',
};

/** La URL pública de una figura, o null para 'none' (o un valor desconocido). */
export function contentFigureUrl(figure) {
  const file = CONTENT_FIGURE_FILES[figure];
  return file ? `/portal/${encodeURIComponent(file)}` : null;
}

/**
 * El fondo en sí, para poner detrás de lo que sea que un contenedor
 * `position: relative` (y `overflow: hidden`, para que no se note si el
 * contenido es más angosto que la figura) dibuje encima. `color` vacío cae en
 * el principal del tema -- mismo criterio que Secundario en portal-theme.js.
 */
export function ContentFigureBackground({ figure, color }) {
  const url = contentFigureUrl(figure);
  if (!url) return null;

  const mask = `url("${url}")`;

  return (
    <Box
      aria-hidden="true"
      sx={{
        position: 'absolute',
        inset: 0,
        pointerEvents: 'none',
        opacity: 0.14,
        backgroundColor: color || 'primary.main',
        WebkitMaskImage: mask,
        maskImage: mask,
        WebkitMaskRepeat: 'no-repeat',
        maskRepeat: 'no-repeat',
        WebkitMaskSize: '100% 100%',
        maskSize: '100% 100%',
      }}
    />
  );
}

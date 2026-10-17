import { useId } from 'react';

/**
 * El <pattern> de puntos que repiten hero.jsx, visuals/visual-frame.jsx y el
 * cierre de landing-page.jsx ("cta-card") -- la misma textura de fondo en
 * los tres lugares donde la landing la necesita, antes copiada suelta en
 * cada uno.
 *
 * useId() y no un id fijo: más de una instancia puede estar montada a la vez
 * (las cinco SectionPanel lo están), y un id de SVG repetido es HTML
 * inválido y puede resolver al patrón equivocado.
 *
 * Devuelve el elemento <pattern> (para poner dentro de un <defs> propio) y
 * el `fill` ya armado como `url(#id)` -- no impone cómo ni dónde se dibuja
 * el <rect> que lo usa, porque eso varía por consumidor: hero.jsx lo pinta
 * solo en una franja, visual-frame.jsx lo pinta de punta a punta.
 *
 * @param {object} options
 * @param {string} options.color - Color del punto (ya resuelto, p. ej. con `alpha()`).
 * @param {number} [options.size=24] - Lado de la celda del patrón.
 * @param {number} [options.radius=1] - Radio del punto.
 */
export function useDotPattern({ color, size = 24, radius = 1 }) {
  const patternId = useId();

  return {
    fill: `url(#${patternId})`,
    pattern: (
      <pattern id={patternId} width={size} height={size} patternUnits="userSpaceOnUse">
        <circle cx={radius} cy={radius} r={radius} fill={color} />
      </pattern>
    ),
  };
}

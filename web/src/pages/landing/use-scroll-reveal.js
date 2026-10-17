import { useEffect, useRef } from 'react';
import gsap from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';
import { sinMovimiento } from 'src/lib/motion';

// Antes se registraba una sola vez en landing-page.jsx, porque esa era la
// única página que usaba ScrollTrigger y sus módulos se cargaban primero.
// Ahora que el patrón vive en este hook y lo usan landing-page.jsx,
// live-competitions.jsx y los cinco visuals/*-visual.jsx, el registro vive
// acá: quien importe el hook se lleva el plugin registrado, sin tener que
// acordarse de hacerlo en la página contenedora. registerPlugin es
// idempotente -- registrarlo más de una vez no hace nada raro.
gsap.registerPlugin(ScrollTrigger);

/**
 * Anima `selector` dentro del elemento que reciba el ref devuelto, al entrar
 * en pantalla -- el mismo patrón que ya se repetía suelto en landing-page.jsx
 * ("cta-card") y live-competitions.jsx ("vivo-card"): gsap.context +
 * ScrollTrigger + revert() en el cleanup, gateado por `sinMovimiento()`.
 *
 * El revert() no es prolijidad: StrictMode monta, desmonta y vuelve a
 * montar, y un tl.kill() a secas deja los elementos en opacity 0 para el
 * segundo montaje (from(opacity:0) sobre algo que ya vale 0). revert() les
 * devuelve el estilo original antes de volver a animar.
 *
 * Los cinco gráficos de sección (visuals/) son la quinta, sexta... novena
 * repetición del mismo patrón -- de ahí que valga la pena centralizarlo acá
 * en vez de copiarlo una vez más por archivo.
 *
 * @param {object} options
 * @param {string} options.selector - Selector CSS de los elementos a animar,
 *   relativo al elemento con el ref devuelto (gsap.context lo scopea solo).
 * @param {object} [options.from] - Props de gsap.from además de las que ya
 *   traen default (y, opacity, duration).
 * @param {object} [options.scrollTrigger] - Overrides de ScrollTrigger; por
 *   default dispara cuando el propio elemento scopeado entra al 85% del viewport.
 * @param {any[]} [options.deps] - Dependencias del efecto (además de correrlo
 *   una sola vez al montar) -- p. ej. cuando el contenido a animar llega async.
 * @returns {import('react').RefObject} Ref para poner en el elemento que
 *   envuelve lo que se quiere animar.
 */
export function useScrollReveal({ selector, from, scrollTrigger, deps = [] }) {
  const scopeRef = useRef(null);

  useEffect(() => {
    // El nodo puede no existir todavía si quien llama renderiza
    // condicionalmente (ver live-competitions.jsx: sin competencias en vivo
    // no monta nada) y este efecto corre antes de que haya algo que animar.
    if (sinMovimiento() || !scopeRef.current) return undefined;

    const ctx = gsap.context(() => {
      gsap.from(selector, {
        y: 40,
        opacity: 0,
        duration: 0.6,
        ...from,
        scrollTrigger: {
          trigger: scopeRef.current,
          start: 'top 85%',
          ...scrollTrigger,
        },
      });
    }, scopeRef);

    return () => ctx.revert();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  return scopeRef;
}

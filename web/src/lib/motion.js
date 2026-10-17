// Compartido por cada pantalla de la landing con animacion propia en GSAP
// (landing-page.jsx, live-competitions.jsx, section-nav.jsx): alguien pidio
// que no se mueva nada, y eso vale mas que la animacion. Una sola
// implementacion para que todas respeten `prefers-reduced-motion` de la
// misma forma. hero.jsx no la necesita -- su animacion es CSS pura y el
// navegador ya aplica la media query solo.
export function sinMovimiento() {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

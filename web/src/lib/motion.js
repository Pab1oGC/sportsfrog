// Compartido por landing-page.jsx y playful-hero.jsx: alguien pidio que no se
// mueva nada, y eso vale mas que la animacion. Una sola implementacion para
// que las dos pantallas respeten `prefers-reduced-motion` de la misma forma.
export function sinMovimiento() {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

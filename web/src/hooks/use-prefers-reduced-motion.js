import { useEffect, useState } from 'react';

/**
 * Si el visitante pidió menos movimiento en el sistema operativo. Se usa
 * para apagar (no acortar a la fuerza) las transiciones que este pase
 * agregó al portal público -el fade de sección/categoría, el Fade del
 * visor de galería- ya que ninguna de ellas es indispensable para
 * entender el contenido.
 */
export function usePrefersReducedMotion() {
  const [reduced, setReduced] = useState(() => {
    try {
      return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    } catch {
      return false;
    }
  });

  useEffect(() => {
    if (!window.matchMedia) return undefined;
    const consulta = window.matchMedia('(prefers-reduced-motion: reduce)');
    const alCambiar = (e) => setReduced(e.matches);
    consulta.addEventListener('change', alCambiar);
    return () => consulta.removeEventListener('change', alCambiar);
  }, []);

  return reduced;
}

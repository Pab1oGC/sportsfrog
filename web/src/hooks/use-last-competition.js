import { useEffect, useRef, useState } from 'react';

const STORAGE_KEY = 'sportfrog:lastCompetitionId';

/**
 * Que competencia mostrar antes de que nadie elija nada.
 *
 * Antes, entrar a Categorias, Equipos, Fixtures... mostraba una pantalla
 * vacia hasta elegir a mano una competencia del select, cada vez, aunque un
 * minuto antes se hubiera elegido la misma en otra pantalla. Esto la recuerda
 * -una sola referencia, compartida entre todas las pantallas que filtran por
 * competencia- y si no hay ninguna recordada todavia, elige la mas relevante
 * en vez de dejar la pantalla en blanco: la que esta en curso, o si ninguna
 * lo esta, la primera de la lista (el backend ya la entrega ordenada por
 * temporada mas reciente primero).
 *
 * @param {Array|undefined} competiciones Lo que devuelve GET /competitions.
 * @param {string} [urlCompId] Un id que vino explicito en la URL (un link
 *   directo, por ejemplo "Nueva categoria" desde Competiciones) — gana
 *   siempre sobre lo recordado, porque una direccion explicita es una
 *   intencion mas fuerte que el ultimo valor usado.
 */
export function useLastCompetition(competiciones, urlCompId) {
  const [compId, setCompIdState] = useState(urlCompId || '');
  const resolved = useRef(false);

  useEffect(() => {
    if (resolved.current || !competiciones || competiciones.length === 0) return;
    resolved.current = true;

    if (urlCompId && competiciones.some((c) => c.id === urlCompId)) {
      setCompIdState(urlCompId);
      return;
    }

    let stored = null;
    try { stored = localStorage.getItem(STORAGE_KEY); } catch { /* privado, bloqueado, etc. */ }

    if (stored && competiciones.some((c) => c.id === stored)) {
      setCompIdState(stored);
      return;
    }

    const enCurso = competiciones.find((c) => c.status === 'in_progress');
    setCompIdState((enCurso || competiciones[0]).id);
  }, [competiciones, urlCompId]);

  const setCompId = (id) => {
    setCompIdState(id);
    try {
      if (id) localStorage.setItem(STORAGE_KEY, id);
      else localStorage.removeItem(STORAGE_KEY);
    } catch { /* privado, bloqueado, etc. */ }
  };

  return [compId, setCompId];
}

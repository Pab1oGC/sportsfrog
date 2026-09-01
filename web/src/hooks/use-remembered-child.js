import { useEffect, useRef, useState } from 'react';

/**
 * La misma idea de useLastCompetition, un nivel mas abajo: que categoria (o
 * que equipo) mostrar en cuanto se elige el padre, en vez de una pantalla
 * vacia hasta elegir tambien eso a mano.
 *
 * A diferencia de la competencia -que es una sola referencia para toda la
 * sesion- lo que se recuerda aca depende de cual es el padre: la categoria
 * elegida en el Torneo A no tiene por que ser la misma que en el Torneo B, y
 * mostrarla igual estaria mostrando una categoria de otra competencia. Por
 * eso la clave de guardado incluye el id del padre.
 *
 * @param {Array|undefined} items Las opciones del hijo (categorias, equipos),
 *   ya filtradas por el padre — undefined mientras todavia se estan pidiendo.
 * @param {string} scopeKey El id del padre (competitionId para una categoria,
 *   categoryId para un equipo). Cambiar esto es lo que hace que se vuelva a
 *   resolver: una eleccion manual dentro del mismo padre no se toca.
 * @param {string} storagePrefix Prefijo de la clave en localStorage, propio
 *   de cada nivel ("sportfrog:lastCategoryId:", "sportfrog:lastTeamId:").
 */
export function useRememberedChild(items, scopeKey, storagePrefix) {
  const [selectedId, setSelectedIdState] = useState('');
  const resolvedFor = useRef(null);

  useEffect(() => {
    if (!scopeKey) {
      resolvedFor.current = null;
      setSelectedIdState('');
      return;
    }
    if (!items || resolvedFor.current === scopeKey) return;
    resolvedFor.current = scopeKey;

    let stored = null;
    try { stored = localStorage.getItem(storagePrefix + scopeKey); } catch { /* privado, bloqueado, etc. */ }

    if (stored && items.some((i) => i.id === stored)) {
      setSelectedIdState(stored);
      return;
    }

    setSelectedIdState(items[0]?.id || '');
  }, [items, scopeKey, storagePrefix]);

  const setSelectedId = (id) => {
    setSelectedIdState(id);
    if (!scopeKey) return;
    try {
      if (id) localStorage.setItem(storagePrefix + scopeKey, id);
      else localStorage.removeItem(storagePrefix + scopeKey);
    } catch { /* privado, bloqueado, etc. */ }
  };

  return [selectedId, setSelectedId];
}

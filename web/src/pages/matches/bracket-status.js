/**
 * Qué tan avanzado está el sorteo de la llave de una categoría, para decidir
 * qué botones ofrece matches-page.jsx ("Siguiente ronda", "Sortear",
 * "Descargar jornada").
 */

/**
 * Hay llave en esta categoría, pero todavía sin final: una promovida antes
 * de que se sortearan todas las rondas de una vez (ver "Siguiente ronda").
 * Con la final ya creada no hay ronda que sortear.
 */
export function quedaRondaPorSortear(matches, catId) {
  const partidosDeLlave = catId
    ? (matches || []).filter((m) => m.categoryId === catId && m.phase && !m.isRepechage)
    : [];
  return partidosDeLlave.length > 0 && !partidosDeLlave.some((m) => m.phase === 'final');
}

/**
 * Las rondas de fase de eliminatoria reusan números desde 1 (ver
 * AdvanceBracket) y no tienen relación con las jornadas de la fase de
 * grupos, así que solo se ofrecen rondas sin fase -- igual criterio que ya
 * usa la columna "Jornada" de la grilla.
 */
export function rondasDisponibles(matches, catId) {
  return catId
    ? [...new Set((matches || [])
        .filter((m) => m.categoryId === catId && !m.phase && m.roundNumber != null)
        .map((m) => m.roundNumber))]
        .sort((a, b) => a - b)
    : [];
}

/**
 * Por qué "Sortear" está deshabilitado ahora mismo, en las palabras exactas
 * que lee quien organiza -- o null si sí se puede sortear. Un motivo
 * concreto en vez de un booleano, igual criterio que problemaDeSlug en
 * lib/slug.js.
 */
export function motivoDeSorteoInhabilitado(comp, yaJugados) {
  if (!comp) return null;
  const sorteable = comp.status === 'draft' || comp.status === 'scheduled';
  if (!sorteable) return 'La competencia ya esta en curso.';
  if (yaJugados > 0) return `Ya hay ${yaJugados} partidos jugados.`;
  return null;
}

/**
 * Los tres predicados que deciden qué botones y qué mensaje muestra
 * PerformancesPage -- fácil de confundir cuál es cuál, por eso viven
 * nombrados aparte en vez de como expresiones sueltas en el componente.
 */

/**
 * Una categoría sin clasificación abierta todavía lee un array vacío, no un
 * 404 -- ver ReadPerformances en el backend. "Abierta" es exactamente eso:
 * hay filas.
 */
export function estaAbierta(rows) {
  return rows.length > 0;
}

/** Todos los competidores tienen puntaje cargado -- habilita sortear la eliminatoria. */
export function estanTodosPuntuados(rows) {
  return estaAbierta(rows) && rows.every((row) => row.status === 'scored');
}

/**
 * Al menos un competidor ya tiene puntaje cargado -- OpenClassificationStage
 * rechaza reabrir en cuanto CUALQUIERA lo tiene (no solo cuando están
 * todos), así que esto deshabilita "(Re)abrir clasificación" por la misma
 * razón que el sorteo de partidos ya deshabilita "Sortear" cuando sabe de
 * antemano que el pedido va a fallar.
 */
export function hayAlgunoPuntuado(rows) {
  return rows.some((row) => row.status === 'scored');
}

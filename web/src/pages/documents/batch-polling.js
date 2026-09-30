/**
 * Un lote de documentos se procesa en segundo plano: el POST contesta "en
 * cola" y nada más vuelve a preguntar por sí solo -- hace falta seguir
 * consultando mientras algo siga en vuelo, y dejar de hacerlo apenas
 * termina. Estas funciones son el "refreshInterval" de SWR: se les pasa lo
 * último que llegó y devuelven cada cuántos milisegundos volver a preguntar,
 * o 0 para apagarse sola.
 */

/** Mientras está en alguno de estos, el lote todavía se mueve solo. */
export var EN_VUELO = { queued: true, running: true };

/** Si algún lote de la lista sigue en vuelo. */
export function hayLoteEnVuelo(batches) {
  return (batches || []).some(function (b) { return EN_VUELO[b.status]; });
}

/** El intervalo de sondeo de la lista de lotes: 2s mientras algo siga en vuelo, apagado si no. */
export function intervaloDeListaDeLotes(ultimo) {
  return hayLoteEnVuelo(ultimo) ? 2000 : 0;
}

/** El intervalo de sondeo del detalle de un lote puntual: 1.5s mientras siga en vuelo. */
export function intervaloDeDetalleDeLote(ultimo) {
  return ultimo && EN_VUELO[ultimo.status] ? 1500 : 0;
}

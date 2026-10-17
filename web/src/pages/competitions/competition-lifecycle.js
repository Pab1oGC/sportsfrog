/**
 * El ciclo de vida de una competencia, visto desde el panel: a qué estado
 * puede pasar cada uno (espejo de CompetitionLifecycle del lado del
 * servidor, que es la fuente de verdad real -- esto solo repite la forma
 * para que el botón correcto aparezca sin que haga falta intentarlo y
 * fallar) y cómo se nombra, colorea e ilustra cada estado.
 *
 * Centralizado porque CompetitionsPage y el resumen de una competencia
 * (overview-page.jsx) necesitan exactamente lo mismo -- antes solo vivía
 * duplicado a mano dentro de la primera.
 */

export const STATUS_LABEL = {
  draft: 'Borrador',
  scheduled: 'Programada',
  in_progress: 'En curso',
  finished: 'Finalizada',
  cancelled: 'Cancelada',
};

export const STATUS_COLOR = {
  draft: 'default',
  scheduled: 'info',
  in_progress: 'warning',
  finished: 'success',
  cancelled: 'error',
};

export const STATUS_ICON = {
  draft: 'eva:edit-fill',
  scheduled: 'eva:calendar-outline',
  in_progress: 'eva:play-circle-fill',
  finished: 'eva:checkmark-circle-fill',
  cancelled: 'eva:close-circle-fill',
};

// Igual que CompetitionLifecycle.Allowed del lado del servidor -- ese es el
// que de verdad decide; esto solo evita ofrecer un botón que el servidor
// rechazaría de entrada.
export const NEXT_STATUS = {
  draft: ['scheduled', 'cancelled'],
  scheduled: ['draft', 'in_progress', 'cancelled'],
  in_progress: ['finished'],
  finished: [],
  cancelled: [],
};

export const FORMAT_LABEL = {
  league: 'Todos vs todos',
  knockout: 'Eliminación',
  groups: 'Grupos',
};

/**
 * Por qué "Pública" no se puede activar todavía, o `null` si no hay ningún
 * motivo. Espeja la única regla que PublishCompetition rechaza del lado del
 * servidor -- una competencia en borrador no tiene fixtures que mostrar --
 * para poder deshabilitar el interruptor con una explicación en vez de
 * dejar que la persona lo intente y reciba un 409.
 *
 * Despublicar, en cambio, el servidor nunca lo rechaza (ver sus propios
 * comentarios), así que esta función tampoco bloquea esa dirección: una
 * competencia ya pública nunca tiene motivo.
 */
export function motivoPublicacionInhabilitada(competition) {
  if (!competition || competition.isPublic || competition.status !== 'draft') return null;

  return 'Pasala a "Programada" primero: una competencia en borrador no tiene fixtures que mostrar.';
}

/**
 * Acciones administrativas del fixture para un partido: walkover (el
 * partido no se juega, se otorga) y reabrir (deshacer cancelado/walkover/
 * aplazado de vuelta a programado). Ninguna de las dos toca lo que paso en
 * cancha -- son estado del calendario, no del juego.
 *
 * Separado de live-actions.js: ver ese archivo para por que conviven en la
 * misma fila pero no en la misma responsabilidad.
 */
export function buildFixtureActions(m, { setSelMatch, setError, setWoOpen, doStatus }) {
  return [
    // Igual que Iniciar en live-actions.js: no hay a quien otorgarselo
    // todavia si esta ronda de una llave sorteada completa no tiene sus dos
    // equipos definidos.
    m.status === 'scheduled' && m.homeTeamId && m.awayTeamId && {
      icon: 'eva:alert-triangle-fill', label: 'Walkover', color: 'warning.main',
      onClick: () => { setSelMatch(m); setError(''); setWoOpen(true); },
    },
    ['cancelled', 'walkover', 'postponed'].includes(m.status) && {
      icon: 'eva:refresh-outline', label: 'Reabrir (vuelve a programado)', color: 'info.main',
      onClick: () => doStatus(m.id, 'scheduled'),
    },
  ];
}

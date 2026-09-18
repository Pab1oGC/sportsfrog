/**
 * Acciones del ciclo de vida en vivo de un partido: arrancarlo, cerrarlo
 * (solo o a mano) y el desempate por penales cuando hace falta. Todo lo que
 * solo tiene sentido una vez que el partido se esta jugando o se acaba de
 * resolver jugando.
 *
 * "Eventos" no vive aca a pesar de ser tan del partido en vivo como el resto:
 * a diferencia de estas, que se disparan una vez por partido, esa se abre
 * una vez por gol/tarjeta/punto -- enterrarla en el mismo "..." que el resto
 * le suma un click de mas cada vez. Vive suelta como primary en
 * matches-page.jsx.
 *
 * Separado de fixture-actions.js (logistica del fixture: reprogramar,
 * walkover, reabrir) porque son dos responsabilidades distintas sobre la
 * misma fila -- una decide que se puede hacer mientras el partido se juega,
 * la otra decide su estado administrativo. Cada una cambia por sus propios
 * motivos (un deporte nuevo con otra forma de puntuar no toca fixture-
 * actions.js, un cambio en como se reprograma no toca este archivo).
 */
// Si el deporte tiene al menos un evento que suma al marcador (goles en
// futbol, punto/gam-jeom en taekwondo kyorugi desde que
// AddTaekwondoKyorugiScoringEvents los activo), "Finalizar" puede cerrar el
// partido con lo ya cargado. No es lo mismo que "no es por sets": un
// deporte por sets puede tener esto (kyorugi) o no (voley), asi que se
// pregunta esto en vez de leer isPlayedInSets.
function tieneEventoDeMarcador(sportInfo) {
  return Array.isArray(sportInfo && sportInfo.metrics)
    && sportInfo.metrics.some(function(metrica) { return metrica.affectsScore; });
}

export function buildLiveActions(m, {
  sportInfo, setSelMatch, setError, setResOpen, setPoOpen, doStatus, doFinishFromEvents,
}) {
  return [
    // homeTeamId/awayTeamId llegan null en una llave de eliminacion directa
    // sorteada completa: la ronda existe con fecha y cancha reservadas antes
    // de saber quien la juega. No se puede arrancar un partido asi todavia.
    m.status === 'scheduled' && m.homeTeamId && m.awayTeamId && {
      icon: 'eva:play-circle-fill', label: 'Iniciar', color: 'warning.main',
      onClick: () => doStatus(m.id, 'in_progress'),
    },

    // Futbol, basquet, taekwondo kyorugi: los goles o los puntos ya se
    // cargaron como eventos mientras se jugaba, asi que "Finalizar" cierra
    // el partido con esa cuenta en un solo click -- pedir el mismo numero
    // otra vez a mano no suma nada. Un deporte por sets sin metricas de
    // marcador (voley, wally) no tiene forma de derivarlo de los eventos,
    // asi que ese sigue pidiendo el resultado a mano. sportInfo &&
    // tieneEventoDeMarcador(sportInfo), no solo la funcion sola: mientras
    // todavia esta cargando (sportInfo undefined) el boton seguro es el
    // manual, igual que antes de tener este campo.
    m.status === 'in_progress' && sportInfo && tieneEventoDeMarcador(sportInfo) && {
      icon: 'eva:checkmark-circle-fill', label: 'Finalizar con el marcador de los eventos', color: 'success.main',
      onClick: () => doFinishFromEvents(m),
    },
    m.status === 'in_progress' && (!sportInfo || !tieneEventoDeMarcador(sportInfo)) && {
      icon: 'eva:checkmark-circle-fill', label: 'Resultado', color: 'success.main',
      onClick: () => { setSelMatch(m); setError(''); setResOpen(true); },
    },

    // Solo tiene sentido en una eliminatoria (fase != null) y con el partido
    // ya empatado: en todo lo demas un empate es un resultado valido y no
    // hay nada que desempatar.
    m.status === 'finished' && m.phase && m.homeTotal === m.awayTotal && {
      icon: 'eva:radio-button-on-outline', label: 'Desempate por penales', color: 'secondary.main',
      onClick: () => { setSelMatch(m); setError(''); setPoOpen(true); },
    },
    m.status === 'in_progress' && {
      icon: 'eva:close-circle-fill', label: 'Cancelar', color: 'error.main',
      onClick: () => doStatus(m.id, 'cancelled'),
    },
  ];
}

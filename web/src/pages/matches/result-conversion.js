import { esJuzgado, aPuntaje } from 'src/lib/sport-shape';

/**
 * El backend guarda cada periodo como {p,h,a} -- una letra por clave porque
 * PeriodScore documenta que son cientos de estos guardados por temporada
 * (ver el tipo en el dominio). ResultDialog lee y escribe {period,home,away}
 * puertas adentro, más legible para el resto del archivo; estas dos
 * funciones son el único lugar donde una forma se convierte en la otra.
 *
 * Sin esta conversión, PUT /matches/{id}/result nunca funcionó: mandaba
 * {period,home,away} contra un contrato que exige p/h/a como campos
 * obligatorios, y System.Text.Json lo rechaza de entrada (verificado contra
 * el tipo real del backend, no asumido). No es parte del modo juzgado -- es
 * un bug preexistente en cualquier deporte, encontrado al tocar este mismo
 * archivo para agregarlo.
 */
export function deApiAPeriodo(p) {
  return { period: p.p, home: p.h, away: p.a };
}
export function aPeriodoApi(p) {
  return { p: p.period, h: p.home, a: p.away };
}

/**
 * Un deporte de suma (fútbol, básquet) siempre juega todos sus periodos
 * configurados: la cantidad del deporte es la cantidad correcta. Uno por
 * sets rara vez llega al máximo (una mejor-de-cinco que termina 3-0 solo jugó
 * tres), así que ahí se arranca en uno y el diálogo deja agregar los que
 * hagan falta. Uno juzgado es siempre una sola actuación por lado
 * (JudgedRulesetShape ya lo exige al guardar el reglamento) -- misma
 * respuesta que ya da un deporte por sets, por la misma razón: no hay más
 * que un periodo que jugar.
 */
export function periodScoresIniciales(selMatch, sportInfo) {
  if (selMatch.periodScores?.length) {
    return selMatch.periodScores.map(deApiAPeriodo);
  }
  const cantidad = (sportInfo?.isPlayedInSets || esJuzgado(sportInfo)) ? 1 : (sportInfo?.defaultPeriods || 2);
  return Array.from({ length: cantidad }, (_, i) => ({ period: i + 1, home: 0, away: 0 }));
}

/**
 * El marcador total del partido, en tres formas según el deporte. Bajo sets
 * el marcador del partido es la cantidad de periodos ganados por cada lado
 * (ver ScoreConsolidation en el backend), no la suma de los puntos de cada
 * set -- sumar 25-20, 22-25, 25-18 no da un número que signifique algo. Bajo
 * juzgado es directamente el puntaje del juez, decodificado (aPuntaje) -- una
 * sola actuación, nada que consolidar. El resto (deportes de suma) es la
 * suma llana de cada periodo.
 */
export function totalDelPartido(sportInfo, periodScores) {
  if (sportInfo?.isPlayedInSets) {
    return {
      home: periodScores.filter((p) => p.home > p.away).length,
      away: periodScores.filter((p) => p.away > p.home).length,
    };
  }
  if (esJuzgado(sportInfo)) {
    return { home: aPuntaje(periodScores[0]?.home), away: aPuntaje(periodScores[0]?.away) };
  }
  return {
    home: periodScores.reduce((s, p) => s + p.home, 0),
    away: periodScores.reduce((s, p) => s + p.away, 0),
  };
}

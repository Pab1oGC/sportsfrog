// Quién ganó, si está en vivo y si se definió por penales — la misma regla
// que Partido y CruceLlave (portal público) necesitaban cada uno por su
// lado, calculada dos veces de forma idéntica. Un solo lugar para esa
// decisión, mismo criterio que ya usan match-status.js y
// tiebreaker-labels.js para sus propias reglas compartidas.
export function resolveMatchOutcome(m) {
  var jugado = m.homeTotal != null && m.awayTotal != null;
  // Mientras el partido esta en curso todavia no hay Resultado cargado
  // (homeTotal/awayTotal siguen null a proposito, hasta que se cierra el
  // partido), pero puede que ya haya goles registrados como eventos: se
  // muestra ese marcador en vivo en su lugar, para no dejar la tarjeta en
  // "vs" mientras se esta jugando.
  var enVivo = !jugado && m.status === 'in_progress' && m.liveHomeTotal != null && m.liveAwayTotal != null;
  // Empatado en los 90 minutos no siempre es empatado en la llave: una
  // eliminatoria que llego a penales ya tiene quien sigue, y ese es el que se
  // resalta, no el resultado del partido en si.
  var huboPenales = jugado && m.homeTotal === m.awayTotal && m.penaltyHomeScore != null;
  var ganoLocal = jugado && (huboPenales ? m.penaltyHomeScore > m.penaltyAwayScore : m.homeTotal > m.awayTotal);
  var ganoVisita = jugado && (huboPenales ? m.penaltyAwayScore > m.penaltyHomeScore : m.awayTotal > m.homeTotal);
  var homeMostrado = jugado ? m.homeTotal : enVivo ? m.liveHomeTotal : null;
  var awayMostrado = jugado ? m.awayTotal : enVivo ? m.liveAwayTotal : null;

  return { jugado: jugado, enVivo: enVivo, huboPenales: huboPenales, ganoLocal: ganoLocal, ganoVisita: ganoVisita, homeMostrado: homeMostrado, awayMostrado: awayMostrado };
}

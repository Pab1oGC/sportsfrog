// La forma de un deporte, leida del mismo objeto que ya devuelve GET /sports
// (o el `comp`/`sportInfo` que cada pantalla arma a partir de el) -- un solo
// lugar para las dos preguntas que taekwondo agrega al catalogo, en vez de
// un `sportInfo?.isIndividual === true` repetido en cada pantalla que lo
// necesite. Mismo motivo que ya tiene tiebreaker-labels.js para
// isPlayedInSets: la pregunta se hace una vez aca, no una vez por pantalla.
//
// Los dos ejes son independientes -- Kyorugi es individual y se juega por
// sets (esJuzgado da false ahi); Poomsae es individual y juzgado. Un
// deporte de equipo futuro podria ser juzgado sin ser individual. Por eso
// son dos funciones, no una sola que devuelva "el modo", igual que el
// backend las representa con dos columnas separadas (sports.is_individual,
// sports.score_mode) y no con una sola.

/**
 * Si este deporte inscribe por deportista (uno, una pareja, un trio) en vez
 * de por club -- ver EnrollIndividual y Sport.IsIndividual en el backend.
 * Decide si una categoria se inscribe con /categories/{id}/individuals o con
 * /categories/{id}/teams.
 */
export function esIndividual(sportInfo) {
  return sportInfo?.isIndividual === true;
}

/**
 * Si este deporte se decide por un puntaje que entregan jueces para una sola
 * actuacion, no por eventos sumados ni por periodos ganados -- ver
 * ScoreMode.Judged en el backend. Poomsae es el unico hoy. Decide si una
 * categoria tiene una etapa de clasificacion que abrir (en vez de una tabla
 * de posiciones) y si un partido carga un puntaje de jueces en vez de un
 * marcador por periodo.
 */
export function esJuzgado(sportInfo) {
  return sportInfo?.scoreMode === 'judged';
}

/**
 * Si al cargar un evento tiene sentido elegir una cantidad. Futbol, futsal y
 * voleibol registran cada gol, tarjeta o punto por separado, con su propio
 * minuto: "cantidad" ahi solo permite juntar dos goles bajo un minuto que
 * no es el de ninguno. Espejo de EventPolicy.OneAtATimeSports en el backend,
 * que ademas lo exige -- ocultar el campo sin eso solo esconderia la regla.
 */
const DEPORTES_DE_A_UNO = ['football', 'futsal', 'volleyball'];

export function registraCantidad(sportInfo) {
  return !DEPORTES_DE_A_UNO.includes(sportInfo?.code);
}

/**
 * Cuanto tiempo adicional se admite por periodo cuando el reglamento no lo
 * dice -- espejo de PeriodClock.DefaultMaxExtraMinutes en el backend.
 */
export const ADICIONAL_POR_DEFECTO = 20;

/**
 * Los minutos que un evento puede llevar segun el periodo en el que se carga,
 * o null si la pregunta no aplica (deporte por sets o juzgado, periodo sin
 * reloj, o todavia sin periodo elegido).
 *
 * En un deporte cuyo reloj sigue corriendo entre periodos el minuto es el del
 * partido: el 2.o tiempo de uno de 45 empieza en el 46, y el 48 del 1.o es
 * 45+3. Espejo de PeriodClock.MinuteWindow, que es lo que el servidor exige --
 * esto solo lo muestra antes de mandar. `sportInfo.periodMinutes` y
 * `periodMaxExtraMinutes` no vienen de /sports: los pone la pantalla desde el
 * reglamento efectivo del partido, que es quien manda (ver matches-page).
 *
 * `desde` es 0 en el primer periodo, igual que el servidor (un evento al
 * pitazo inicial); `regular` es donde termina el tiempo reglamentario y
 * `hasta` incluye el adicional.
 */
export function ventanaDeMinuto(sportInfo, periodo) {
  const duracion = sportInfo?.periodMinutes;
  if (sportInfo?.scoreMode !== 'cumulative' || !duracion || !periodo) return null;

  const adicional = sportInfo.periodMaxExtraMinutes ?? ADICIONAL_POR_DEFECTO;
  return {
    desde: periodo === 1 ? 0 : (periodo - 1) * duracion + 1,
    regular: periodo * duracion,
    hasta: periodo * duracion + adicional,
    adicional,
  };
}

/**
 * El entero que guarda el backend para un puntaje de jueces, a texto para
 * mostrar: 765 -> "7.65". Ver PeriodScore en el backend: "the scale is a
 * presentation fact, not a domain one" -- el backend nunca hace esta
 * cuenta, asi que si no se hace aca un puntaje se ve, se imprime o se
 * compara ×100 sin que nada lo avise.
 *
 * `null`/`undefined` (todavia sin puntaje) da '', no "0.00" -- un campo de
 * formulario vacio, no un puntaje de cero que nadie cargo.
 */
export function aPuntaje(entero) {
  if (entero === null || entero === undefined) return '';
  return (entero / 100).toFixed(2);
}

/**
 * El inverso de aPuntaje, para mandar al backend lo que alguien tipeo:
 * "7.65" -> 765. Math.round, no un truncado ni una multiplicacion directa,
 * porque 7.65 * 100 da 764.9999999999999 en punto flotante -- el mismo
 * numero que un truncado convertiria en 764.
 *
 * Vacio, en blanco, o algo no numerico da `null` (nada cargado todavia),
 * nunca 0 -- distincion que le importa a RecordPerformance: un 0 es un
 * puntaje real, y un puntaje real vuelve el partido `Scored`.
 */
export function dePuntaje(texto) {
  if (texto === null || texto === undefined) return null;
  const limpio = String(texto).trim();
  if (limpio === '') return null;
  const numero = Number(limpio);
  return Number.isNaN(numero) ? null : Math.round(numero * 100);
}

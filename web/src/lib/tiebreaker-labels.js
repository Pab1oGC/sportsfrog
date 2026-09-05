// Mismos codigos que SportFrog.Domain.Rules.Tiebreaker. Compartido entre el
// formulario de reglamentos y el portal publico -- antes cada uno tenia su
// propia copia de este mapa, las dos escritas fijas para futbol ("Diferencia
// de gol"), asi que un torneo de voley las mostraba igual de mal en los dos
// lugares.
//
// score_difference/score_for/score_against dependen de que cuenta este
// deporte -- goles en uno de suma, sets en uno por sets -- asi que se arman
// con lo que /sports ya expone (scoringUnit, periodLabel) en vez de venir
// fijas a "gol".
export const TIEBREAKER_CODES = ['score_difference', 'score_for', 'score_against', 'wins', 'head_to_head'];

// Plural conocido de las unidades que hoy trae el catalogo. Un "+s" simple
// alcanza para "punto" y "set", pero no para "gol" (goles, no "gols") --
// mismo tipo de limite que ya acepta PeriodLabel.Plural en el backend: una
// regla explicita para las palabras que existen, no una teoria general del
// plural en español.
const PLURALES_CONOCIDOS = { gol: 'goles' };
function plural(palabra) {
  return PLURALES_CONOCIDOS[palabra] || (palabra.endsWith('s') ? palabra : palabra + 's');
}
function capitalizar(palabra) {
  return palabra.charAt(0).toUpperCase() + palabra.slice(1);
}

/**
 * Lo que cuenta este deporte, en plural: "goles", "puntos", "sets". La misma
 * palabra que arma tanto las etiquetas de desempate como las columnas de la
 * tabla de posiciones (GF/GC), para que las dos digan siempre lo mismo.
 * Sin `sportInfo` todavia cargado, cae en "puntos" — el mismo texto generico
 * que ya tenia esto antes de saber el deporte.
 */
export function unidadDeMarcador(sportInfo) {
  var esPorSets = Boolean(sportInfo && sportInfo.isPlayedInSets);
  var singular = esPorSets ? ((sportInfo && sportInfo.periodLabel) || 'set') : ((sportInfo && sportInfo.scoringUnit) || 'punto');
  return plural(singular.toLowerCase());
}

// Como se habla de cuanto tiene cada lado: en un deporte de suma es "a
// favor"/"en contra" (goles a favor, la forma futbolera). En uno por sets
// nadie dice "sets a favor" -- se habla de sets ganados y perdidos, la
// misma palabra que ya separa a un equipo del otro en el marcador de un
// partido. Una sola función arma las dos formas (larga, para el texto de
// desempate; la inicial, para la columna) para que no puedan desalinearse.
function calificativos(sportInfo) {
  return Boolean(sportInfo && sportInfo.isPlayedInSets)
    ? { favor: 'ganados', contra: 'perdidos', inicialFavor: 'G', inicialContra: 'P' }
    : { favor: 'a favor', contra: 'en contra', inicialFavor: 'F', inicialContra: 'C' };
}

/**
 * Las etiquetas de los criterios de desempate, para el deporte de `sportInfo`
 * (el elemento que devuelve /sports, o el `comp` del portal publico — los dos
 * exponen isPlayedInSets/scoringUnit/periodLabel con el mismo nombre).
 */
export function etiquetasDesempate(sportInfo) {
  var unidad = unidadDeMarcador(sportInfo);
  var cal = calificativos(sportInfo);

  return {
    score_difference: 'Diferencia de ' + unidad,
    score_for: capitalizar(unidad) + ' ' + cal.favor,
    score_against: capitalizar(unidad) + ' ' + cal.contra,
    wins: 'Partidos ganados',
    head_to_head: 'Enfrentamiento directo',
  };
}

/**
 * Los encabezados de las columnas "a favor"/"en contra" de una tabla de
 * posiciones: GF/GC en un deporte de suma, SG/SP ("sets ganados"/"sets
 * perdidos") en uno por sets — la abreviatura real de vóley, no una genérica
 * armada con "a favor". La misma palabra y el mismo criterio que
 * `etiquetasDesempate`, para que la columna y el texto de desempate nunca
 * digan cosas distintas.
 */
export function columnasMarcador(sportInfo) {
  var inicial = unidadDeMarcador(sportInfo).charAt(0).toUpperCase();
  var cal = calificativos(sportInfo);
  return { favor: inicial + cal.inicialFavor, contra: inicial + cal.inicialContra };
}

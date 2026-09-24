import { FASES } from 'src/lib/phase-labels';
import { PENDIENTE } from 'src/lib/match-status';

/**
 * Agrupa partidos por jornada (si no tienen fase) o por fase de
 * eliminatoria (si la tienen), en el orden en que se juegan. Compartida
 * por `CalendarView` (la lista) y `BracketView` (la llave, filtrando acá
 * los grupos de fase) -- las dos necesitan el mismo orden, y antes solo
 * vivía dentro de CalendarView, enredada con el filtro "por equipo" que
 * a la llave no le hace falta.
 *
 * Cada grupo devuelto: { clave, titulo, matches, pendientes, jugados,
 * terminada, enCurso }. `clave` empieza con "f:" para un grupo de fase
 * (eliminatoria) y con "j:"/"sin" para una jornada de grupos -- así se
 * filtran los de fase sin tener que volver a mirar cada partido.
 *
 * Un grupo de fase lleva también la ronda en su clave (`f:<ronda>:<fase>`),
 * no solo el nombre de la fase: dos rondas de una misma llave nunca deberían
 * compartir nombre, pero si alguna vez lo hacen -- un cruce con byes puede
 * dejar la ronda uno con tan pocos partidos reales como una semifinal --
 * esto evita mezclar sus partidos en una sola columna. Verían el mismo
 * título dos veces antes que un cruce ya definido revuelto con uno todavía
 * por confirmar.
 */
export function agruparPorRonda(fixtures) {
  // La fase manda antes que la ronda: una categoria que paso de grupos a
  // eliminatoria vuelve a empezar su numeracion de ronda en el cruce, igual
  // que la volvio a empezar la propia fase de grupos -asi que "ronda 1" sola
  // no distingue la primera jornada de grupos del primer cruce de la llave.
  // La fase si distingue: es null en toda la fase de grupos y no-null en
  // toda la eliminatoria.
  var orden = fixtures.slice().sort(function(a, b) {
    var faseA = a.phase ? 1 : 0;
    var faseB = b.phase ? 1 : 0;
    if (faseA !== faseB) return faseA - faseB;
    var ra = a.roundNumber == null ? 9999 : a.roundNumber;
    var rb = b.roundNumber == null ? 9999 : b.roundNumber;
    if (ra !== rb) return ra - rb;
    if (a.scheduledAt && !b.scheduledAt) return -1;
    if (!a.scheduledAt && b.scheduledAt) return 1;
    if (a.scheduledAt && b.scheduledAt) return new Date(a.scheduledAt) - new Date(b.scheduledAt);
    return 0;
  });

  var grupos = [];
  var porClave = {};
  orden.forEach(function(m) {
    var clave = m.phase ? 'f:' + m.roundNumber + ':' + m.phase : (m.roundNumber != null ? 'j:' + m.roundNumber : 'sin');
    if (!porClave[clave]) {
      porClave[clave] = {
        clave: clave,
        titulo: m.phase
          ? (FASES[m.phase] || m.phase)
          : (m.roundNumber != null ? 'Jornada ' + m.roundNumber : 'Sin jornada'),
        matches: []
      };
      grupos.push(porClave[clave]);
    }
    porClave[clave].matches.push(m);
  });

  grupos.forEach(function(g) {
    g.pendientes = g.matches.filter(function(m) { return PENDIENTE[m.status]; }).length;
    g.jugados = g.matches.filter(function(m) { return m.homeTotal != null && m.awayTotal != null; }).length;
    g.terminada = g.pendientes === 0;
    g.enCurso = g.pendientes > 0 && g.jugados > 0;
  });

  return grupos;
}

/** Los grupos que son de fase eliminatoria (llave), no jornadas de grupos. */
export function soloFaseEliminatoria(grupos) {
  return grupos.filter(function(g) { return g.clave.indexOf('f:') === 0; });
}

/**
 * El repechaje de kyorugi, en dos secciones fijas -- una por cada mitad de
 * la llave -- en vez de intercalado entre las jornadas y fases del cuadro
 * principal por `agruparPorRonda`. Sus partidos también llevan `phase` y su
 * propia `roundNumber` (ver `Repechage`/`DrawRepechage` del lado del
 * servidor), numerada desde 1 dentro de cada mitad y sin relación con la
 * ronda del cuadro principal -- agruparlos junto a esas rondas es lo que
 * desordenaba el calendario: una ronda 1 de repechaje caía bajo el mismo
 * encabezado que una ronda 1 de octavos que no tiene nada que ver.
 *
 * `repechageBranch` (1 o 2) es la única señal confiable de a qué mitad
 * pertenece cada partido -- la decide `DrawRepechage` al sortear cada mitad,
 * una vez, y no se puede reconstruir de forma confiable del lado del
 * cliente. Un partido de una versión anterior al campo (sin `repechageBranch`)
 * no entra en ninguna de las dos secciones -- se pierde antes que mostrarse
 * bajo la mitad equivocada.
 */
export function agruparRepechajePorRama(fixtures) {
  var ramas = { 1: [], 2: [] };
  fixtures.forEach(function(m) {
    if (ramas[m.repechageBranch]) ramas[m.repechageBranch].push(m);
  });

  var titulos = { 1: 'Repechaje Bronce A', 2: 'Repechaje Bronce B' };

  return [1, 2]
    .filter(function(rama) { return ramas[rama].length > 0; })
    .map(function(rama) {
      var matches = ramas[rama].slice().sort(function(a, b) { return (a.roundNumber || 0) - (b.roundNumber || 0); });
      var pendientes = matches.filter(function(m) { return PENDIENTE[m.status]; }).length;
      var jugados = matches.filter(function(m) { return m.homeTotal != null && m.awayTotal != null; }).length;

      return {
        clave: 'r:' + rama,
        titulo: titulos[rama],
        matches: matches,
        pendientes: pendientes,
        jugados: jugados,
        terminada: pendientes === 0,
        enCurso: pendientes > 0 && jugados > 0,
      };
    });
}

/**
 * Todo agrupado para el calendario: jornadas y fases del cuadro principal
 * (nunca incluyen un partido de repechaje), con las dos secciones fijas del
 * repechaje intercaladas justo antes de la final -- nunca después. La final
 * es el chip que cierra el calendario de cualquier categoría, con o sin
 * repechaje; el repechaje se juega alrededor de ella, no es lo último que
 * pasa en el torneo, así que su lugar es antes, no después.
 *
 * `agruparPorRonda(normales)` ya deja la final como su último grupo siempre
 * que hay uno -- es el de mayor ronda entre los que sí tienen fase -- así
 * que alcanza con separarlo del resto antes de intercalar el repechaje.
 * Un solo punto de entrada para `CalendarView`, para no repetir en cada
 * lugar que lo usa el filtro de `isRepechage` que separa las dos
 * agrupaciones.
 */
export function agruparCalendario(fixtures) {
  var normales = fixtures.filter(function(m) { return !m.isRepechage; });
  var repechaje = fixtures.filter(function(m) { return m.isRepechage; });
  var principales = agruparPorRonda(normales);
  var deRepechaje = agruparRepechajePorRama(repechaje);

  if (deRepechaje.length === 0 || principales.length === 0) {
    return principales.concat(deRepechaje);
  }

  var final = principales[principales.length - 1];
  var antesDeLaFinal = principales.slice(0, principales.length - 1);
  return antesDeLaFinal.concat(deRepechaje, [final]);
}

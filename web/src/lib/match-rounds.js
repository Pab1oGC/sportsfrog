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

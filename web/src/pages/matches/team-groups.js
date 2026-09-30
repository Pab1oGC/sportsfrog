/**
 * El grupo no es un dato del partido, es un dato del equipo -- se arma acá,
 * por equipo local, para que la grilla de matches-page.jsx no mezcle sin
 * avisar los partidos de grupos distintos bajo la misma "ronda" (cada grupo
 * tiene su propia ronda 1, ronda 2...).
 */
export function mapaDeGrupoPorEquipo(teams) {
  const grupoPorEquipo = {};
  (teams || []).forEach((t) => { grupoPorEquipo[t.id] = t.groupLabel; });
  return grupoPorEquipo;
}

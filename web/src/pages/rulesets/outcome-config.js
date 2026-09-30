/**
 * Mismo cálculo que SportFrog.Domain.Rules.SetsMatchOutcomeRules.RequiredOutcomes
 * en el backend: un partido a la mejor de `count` se gana en toWin =
 * ceil(count/2), y cada desenlace posible se nombra desde los dos lados.
 * Se recalcula acá (en vez de pedírselo al backend) porque `count` cambia
 * mientras el organizador todavía está escribiendo el formulario -- lo que
 * terminará guardado siempre pasa por RulesetPolicy en el servidor, así que
 * una diferencia acá en el peor caso muestra el campo equivocado, nunca
 * guarda un reglamento inválido.
 */
export function desenlacesDeSets(count) {
  const toWin = Math.ceil((count || 1) / 2);
  const desenlaces = [];
  for (let lost = 0; lost < toWin; lost++) {
    desenlaces.push(`win_${toWin}_${lost}`);
    desenlaces.push(`loss_${lost}_${toWin}`);
  }
  return desenlaces;
}

/** "win_2_0" -> "Pts 2-0 (ganado)"; "loss_0_2" -> "Pts 0-2 (perdido)". */
export function etiquetaDesenlace(code) {
  if (code === 'win') return 'Pts victoria';
  if (code === 'draw') return 'Pts empate';
  if (code === 'loss') return 'Pts derrota';
  const m = /^(win|loss)_(\d+)_(\d+)$/.exec(code);
  if (!m) return code;
  const [, resultado, propio, rival] = m;
  return `Pts ${propio}-${rival} (${resultado === 'win' ? 'ganado' : 'perdido'})`;
}

/**
 * Escribe `value` en `config` en la ruta punteada `path` (p. ej.
 * "points.win_2_0"), sin mutar `config` -- devuelve un config nuevo.
 *
 * DEFECTO CONOCIDO, encontrado al extraer esta función (no corregido acá a
 * propósito, ver el comentario del archivo de prueba): para una ruta de DOS
 * O MÁS niveles, el valor se pierde en silencio. El bucle copia
 * `obj[keys[i]]` a una variable local (`obj = { ...obj[keys[i]] }`) pero
 * nunca escribe esa copia de vuelta en su padre antes de seguir bajando --
 * así que la mutación final (`obj[última clave] = value`) cae sobre un
 * objeto ya desconectado de `c`, y `setForm` termina guardando el config
 * de siempre, sin el cambio. Un path de un solo nivel ("points", "walkover",
 * "tiebreakers") no pasa por el bucle y sí funciona -- es exactamente lo que
 * usa toda la página salvo un único lugar: el campo de puntaje por
 * desenlace, `updateConfig(\`points.${code}\`, ...)` en rulesets-page.jsx,
 * que hoy no puede editarse.
 */
export function actualizarConfig(config, path, value) {
  const c = { ...config };
  const keys = path.split('.');
  let obj = c;
  for (let i = 0; i < keys.length - 1; i++) obj = { ...obj[keys[i]] };
  obj[keys[keys.length - 1]] = value;
  return c;
}

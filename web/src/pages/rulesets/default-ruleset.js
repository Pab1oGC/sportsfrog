// Fallback para un deporte sin reloj (volleyball, taekwondo poomsae): el
// reglamento exige una duración estimada del partido cuando no hay minutos
// de período que sumar (ver RulesetPolicy.InspectDuration, del lado del
// servidor). El catálogo de deportes no trae un valor para esto -- a
// diferencia de defaultMinutes/defaultBreakMinutes, es terreno exclusivo del
// reglamento -- así que se usa el mismo número que ya usan los tests del
// dominio para este mismo caso.
const ESTIMATED_MINUTES_SIN_RELOJ = 30;

// Los mismos dos desempates con los que arranca un reglamento nuevo en el
// formulario (ver EMPTY_FORM en rulesets-page.jsx). Siempre válidos: ningún
// desempate está condicionado al deporte, y RulesetShapeValidator los exige
// sin duplicados y conocidos nada más -- vacío de tabla de posiciones
// (Points = {}) incluido.
const DEFAULT_TIEBREAKERS = ['score_difference', 'head_to_head'];

/**
 * El reglamento "en blanco" que armaría quien elige un deporte en el
 * formulario de Reglamentos y no toca nada más: sin tabla de posiciones
 * (Points vacío es una elección válida, no "falta completar" -- ver
 * RulesetPolicy.InspectPoints) y con los períodos que el catálogo del
 * deporte ya sugiere. A diferencia de ese formulario, esto se puede armar
 * sin que haya una persona completando nada: todo sale de `sport`.
 */
export function buildDefaultRulesetConfig(sport) {
  return {
    periods: {
      count: sport.defaultPeriods,
      label: sport.periodLabel.charAt(0).toUpperCase() + sport.periodLabel.slice(1),
      minutes: sport.periodHasClock ? (sport.defaultMinutes ?? null) : null,
      breakMinutes: sport.periodHasClock ? (sport.defaultBreakMinutes ?? null) : null,
      estimatedMinutes: sport.periodHasClock ? null : ESTIMATED_MINUTES_SIN_RELOJ,
    },
    points: {},
    tiebreakers: DEFAULT_TIEBREAKERS,
  };
}

/**
 * Nombre legible para el reglamento que se crea solo. Único dentro de la
 * organización porque CreateRuleset ya lo exige -- si una organización
 * tuviera, por lo que sea, un reglamento con exactamente este nombre sin
 * ser el de este deporte, el alta de la competencia avisa el conflicto en
 * vez de duplicar nada.
 */
export function defaultRulesetName(sport) {
  return `${sport.name} — Reglamento estándar`;
}

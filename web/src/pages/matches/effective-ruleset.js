/**
 * Cuántos períodos tiene un partido y cómo se llaman lo decide el
 * reglamento, no el deporte: `sportInfo.defaultPeriods` es solo el valor con
 * el que arranca un reglamento nuevo, así que un torneo de un solo tiempo
 * seguía ofreciendo dos. El reglamento efectivo es el propio de la
 * categoría si lo tiene y el de la competencia si no -- la misma resolución
 * que hace el servidor al validar (EventPolicy, MatchRulesLookup).
 */
export function resolverReglamentoEfectivo({ jornadaCategorias, selMatch, reglamentos, comp, sportInfo }) {
  const categoriaDelPartido = (jornadaCategorias || []).find((c) => c.id === selMatch?.categoryId);
  const reglamentoDelPartido = (reglamentos || []).find(
    (r) => r.id === (categoriaDelPartido?.effectiveRulesetId ?? comp?.rulesetId),
  );
  const periodosDelPartido = reglamentoDelPartido?.config?.periods;
  const sportInfoDelPartido = sportInfo && periodosDelPartido?.count
    ? {
        ...sportInfo,
        defaultPeriods: periodosDelPartido.count,
        periodLabel: periodosDelPartido.label || sportInfo.periodLabel,
        // Para acotar el minuto de un evento al periodo elegido (ver
        // ventanaDeMinuto): lo que dura un periodo y cuanto adicional admite
        // este reglamento.
        periodMinutes: periodosDelPartido.minutes ?? null,
        periodMaxExtraMinutes: periodosDelPartido.maxExtraMinutes ?? null,
      }
    : sportInfo;

  return { categoriaDelPartido, reglamentoDelPartido, periodosDelPartido, sportInfoDelPartido };
}

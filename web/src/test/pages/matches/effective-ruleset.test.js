import { describe, expect, it } from "vitest";
import { resolverReglamentoEfectivo } from "src/pages/matches/effective-ruleset";

const sportInfo = { code: "football", scoringUnit: "gol", periodLabel: "tiempo", defaultPeriods: 2 };

describe("resolverReglamentoEfectivo", () => {
  it("usa el reglamento propio de la categoría del partido cuando lo tiene", () => {
    const { reglamentoDelPartido } = resolverReglamentoEfectivo({
      selMatch: { categoryId: "cat1" },
      jornadaCategorias: [{ id: "cat1", effectiveRulesetId: "r-categoria" }],
      reglamentos: [{ id: "r-categoria", config: {} }, { id: "r-competencia", config: {} }],
      comp: { rulesetId: "r-competencia" },
      sportInfo,
    });
    expect(reglamentoDelPartido.id).toBe("r-categoria");
  });

  it("sin reglamento propio de la categoría, cae en el de la competencia", () => {
    const { reglamentoDelPartido } = resolverReglamentoEfectivo({
      selMatch: { categoryId: "cat1" },
      jornadaCategorias: [{ id: "cat1", effectiveRulesetId: null }],
      reglamentos: [{ id: "r-competencia", config: {} }],
      comp: { rulesetId: "r-competencia" },
      sportInfo,
    });
    expect(reglamentoDelPartido.id).toBe("r-competencia");
  });

  it("sin selMatch todavía (ningún diálogo abierto), no revienta y no resuelve nada", () => {
    const resultado = resolverReglamentoEfectivo({
      selMatch: null,
      jornadaCategorias: [],
      reglamentos: [],
      comp: null,
      sportInfo: undefined,
    });
    expect(resultado.categoriaDelPartido).toBeUndefined();
    expect(resultado.reglamentoDelPartido).toBeUndefined();
    expect(resultado.sportInfoDelPartido).toBeUndefined();
  });

  it("sin ningún reglamento que coincida, sportInfoDelPartido cae en el sportInfo tal cual (sin personalizar)", () => {
    const { sportInfoDelPartido } = resolverReglamentoEfectivo({
      selMatch: { categoryId: "cat1" },
      jornadaCategorias: [{ id: "cat1" }],
      reglamentos: [],
      comp: {},
      sportInfo,
    });
    expect(sportInfoDelPartido).toBe(sportInfo);
  });

  it("con un reglamento que define períodos, personaliza defaultPeriods/periodLabel/periodMinutes sobre el sportInfo del deporte", () => {
    const { sportInfoDelPartido } = resolverReglamentoEfectivo({
      selMatch: { categoryId: "cat1" },
      jornadaCategorias: [{ id: "cat1", effectiveRulesetId: "r1" }],
      reglamentos: [{ id: "r1", config: { periods: { count: 3, label: "Set", minutes: 25, maxExtraMinutes: 5 } } }],
      comp: {},
      sportInfo,
    });

    expect(sportInfoDelPartido).toEqual({
      ...sportInfo,
      defaultPeriods: 3,
      periodLabel: "Set",
      periodMinutes: 25,
      periodMaxExtraMinutes: 5,
    });
  });

  it("un reglamento con período pero sin label propio conserva el periodLabel del deporte", () => {
    const { sportInfoDelPartido } = resolverReglamentoEfectivo({
      selMatch: { categoryId: "cat1" },
      jornadaCategorias: [{ id: "cat1", effectiveRulesetId: "r1" }],
      reglamentos: [{ id: "r1", config: { periods: { count: 3 } } }],
      comp: {},
      sportInfo,
    });
    expect(sportInfoDelPartido.periodLabel).toBe(sportInfo.periodLabel);
  });

  it("sin periodMinutes/periodMaxExtraMinutes en el reglamento, quedan en null -- no undefined ni heredados del deporte", () => {
    const { sportInfoDelPartido } = resolverReglamentoEfectivo({
      selMatch: { categoryId: "cat1" },
      jornadaCategorias: [{ id: "cat1", effectiveRulesetId: "r1" }],
      reglamentos: [{ id: "r1", config: { periods: { count: 3 } } }],
      comp: {},
      sportInfo,
    });
    expect(sportInfoDelPartido.periodMinutes).toBeNull();
    expect(sportInfoDelPartido.periodMaxExtraMinutes).toBeNull();
  });

  it("un reglamento sin sección 'periods' (count ausente) no personaliza nada -- sportInfo intacto", () => {
    const { sportInfoDelPartido } = resolverReglamentoEfectivo({
      selMatch: { categoryId: "cat1" },
      jornadaCategorias: [{ id: "cat1", effectiveRulesetId: "r1" }],
      reglamentos: [{ id: "r1", config: {} }],
      comp: {},
      sportInfo,
    });
    expect(sportInfoDelPartido).toBe(sportInfo);
  });

  it("sin sportInfo todavía cargado (undefined), sportInfoDelPartido queda undefined aunque haya reglamento", () => {
    const { sportInfoDelPartido } = resolverReglamentoEfectivo({
      selMatch: { categoryId: "cat1" },
      jornadaCategorias: [{ id: "cat1", effectiveRulesetId: "r1" }],
      reglamentos: [{ id: "r1", config: { periods: { count: 3 } } }],
      comp: {},
      sportInfo: undefined,
    });
    expect(sportInfoDelPartido).toBeUndefined();
  });
});

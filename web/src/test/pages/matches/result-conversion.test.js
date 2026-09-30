import { describe, expect, it } from "vitest";
import { deApiAPeriodo, aPeriodoApi, periodScoresIniciales, totalDelPartido } from "src/pages/matches/result-conversion";

describe("deApiAPeriodo / aPeriodoApi -- el adaptador de cable {p,h,a} <-> {period,home,away}", () => {
  it("deApiAPeriodo traduce del contrato del backend a la forma legible", () => {
    expect(deApiAPeriodo({ p: 1, h: 3, a: 2 })).toEqual({ period: 1, home: 3, away: 2 });
  });

  it("aPeriodoApi hace el camino inverso, exacto -- el contrato exige p/h/a, no period/home/away", () => {
    expect(aPeriodoApi({ period: 1, home: 3, away: 2 })).toEqual({ p: 1, h: 3, a: 2 });
  });

  it("son inversas exactas una de la otra", () => {
    const original = { p: 4, h: 0, a: 1 };
    expect(aPeriodoApi(deApiAPeriodo(original))).toEqual(original);
  });
});

describe("periodScoresIniciales", () => {
  it("si el partido ya trae periodScores cargados, los usa (traducidos), no arranca de cero", () => {
    const selMatch = { periodScores: [{ p: 1, h: 2, a: 1 }, { p: 2, h: 0, a: 0 }] };
    expect(periodScoresIniciales(selMatch, {})).toEqual([
      { period: 1, home: 2, away: 1 },
      { period: 2, home: 0, away: 0 },
    ]);
  });

  it("un deporte de suma sin nada cargado arranca con sportInfo.defaultPeriods en 0-0", () => {
    const resultado = periodScoresIniciales({ periodScores: [] }, { defaultPeriods: 3 });
    expect(resultado).toEqual([
      { period: 1, home: 0, away: 0 },
      { period: 2, home: 0, away: 0 },
      { period: 3, home: 0, away: 0 },
    ]);
  });

  it("sin defaultPeriods declarado, cae en 2 (el default de siempre)", () => {
    expect(periodScoresIniciales({ periodScores: [] }, {})).toHaveLength(2);
  });

  it("un deporte por sets arranca con un solo período, sin importar defaultPeriods", () => {
    const resultado = periodScoresIniciales({ periodScores: [] }, { isPlayedInSets: true, defaultPeriods: 5 });
    expect(resultado).toEqual([{ period: 1, home: 0, away: 0 }]);
  });

  it("un deporte juzgado también arranca con un solo período -- una sola actuación por lado", () => {
    const resultado = periodScoresIniciales({ periodScores: [] }, { scoreMode: "judged", defaultPeriods: 5 });
    expect(resultado).toEqual([{ period: 1, home: 0, away: 0 }]);
  });

  it("selMatch.periodScores ausente (undefined, no []) también cae en el arranque por defecto", () => {
    expect(periodScoresIniciales({}, { defaultPeriods: 2 })).toHaveLength(2);
  });
});

describe("totalDelPartido", () => {
  it("deporte por sets: el total es la cantidad de periodos ganados por cada lado, no la suma de puntos", () => {
    const periodScores = [
      { home: 25, away: 20 }, // gana local
      { home: 22, away: 25 }, // gana visita
      { home: 25, away: 18 }, // gana local
    ];
    expect(totalDelPartido({ isPlayedInSets: true }, periodScores)).toEqual({ home: 2, away: 1 });
  });

  it("un set empatado (no debería pasar, pero por las dudas) no cuenta para ningún lado", () => {
    const periodScores = [{ home: 25, away: 25 }];
    expect(totalDelPartido({ isPlayedInSets: true }, periodScores)).toEqual({ home: 0, away: 0 });
  });

  it("deporte juzgado: el total es el puntaje del juez ya decodificado (aPuntaje), no la suma de periodos", () => {
    const periodScores = [{ home: 765, away: 740 }];
    expect(totalDelPartido({ scoreMode: "judged" }, periodScores)).toEqual({ home: "7.65", away: "7.40" });
  });

  it("deporte de suma: el total es la suma llana de cada periodo", () => {
    const periodScores = [
      { home: 1, away: 0 },
      { home: 2, away: 1 },
    ];
    expect(totalDelPartido({}, periodScores)).toEqual({ home: 3, away: 1 });
  });

  it("sin sportInfo todavía (undefined), cae en el camino de suma llana", () => {
    const periodScores = [{ home: 1, away: 2 }];
    expect(totalDelPartido(undefined, periodScores)).toEqual({ home: 1, away: 2 });
  });
});

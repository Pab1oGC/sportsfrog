import { describe, expect, it } from "vitest";
import { TIEBREAKER_CODES, unidadDeMarcador, etiquetasDesempate, columnasMarcador } from "./tiebreaker-labels";

describe("TIEBREAKER_CODES", () => {
  it("expone los cinco códigos que espeja SportFrog.Domain.Rules.Tiebreaker", () => {
    expect(TIEBREAKER_CODES).toEqual(["score_difference", "score_for", "score_against", "wins", "head_to_head"]);
  });
});

describe("unidadDeMarcador", () => {
  it("sin sportInfo cargado todavía, cae en 'puntos' (el texto genérico de siempre)", () => {
    expect(unidadDeMarcador(undefined)).toBe("puntos");
    expect(unidadDeMarcador(null)).toBe("puntos");
  });

  it("deporte de suma sin isPlayedInSets usa scoringUnit, en plural", () => {
    expect(unidadDeMarcador({ scoringUnit: "punto" })).toBe("puntos");
  });

  it("'gol' tiene un plural irregular: 'goles', no 'gols'", () => {
    expect(unidadDeMarcador({ scoringUnit: "gol" })).toBe("goles");
  });

  it("normaliza a minúsculas antes de pluralizar", () => {
    expect(unidadDeMarcador({ scoringUnit: "GOL" })).toBe("goles");
  });

  it("scoringUnit ausente en un deporte de suma cae en 'punto' -> 'puntos'", () => {
    expect(unidadDeMarcador({})).toBe("puntos");
  });

  it("una palabra ya terminada en 's' no se pluraliza dos veces", () => {
    expect(unidadDeMarcador({ scoringUnit: "puntos" })).toBe("puntos");
  });

  it("una palabra sin plural irregular conocido usa la regla simple '+s'", () => {
    expect(unidadDeMarcador({ scoringUnit: "carrera" })).toBe("carreras");
  });

  it("deporte por sets usa periodLabel en vez de scoringUnit", () => {
    expect(unidadDeMarcador({ isPlayedInSets: true, periodLabel: "set" })).toBe("sets");
  });

  it("deporte por sets sin periodLabel cae en 'set' -> 'sets'", () => {
    expect(unidadDeMarcador({ isPlayedInSets: true })).toBe("sets");
  });

  it("isPlayedInSets falsy (0, '', false) se trata igual que ausente: usa scoringUnit", () => {
    expect(unidadDeMarcador({ isPlayedInSets: false, scoringUnit: "punto" })).toBe("puntos");
  });
});

describe("etiquetasDesempate", () => {
  it("deporte de suma (fútbol): 'a favor'/'en contra', la forma futbolera", () => {
    const etiquetas = etiquetasDesempate({ scoringUnit: "gol" });
    expect(etiquetas).toEqual({
      score_difference: "Diferencia de goles",
      score_for: "Goles a favor",
      score_against: "Goles en contra",
      wins: "Partidos ganados",
      head_to_head: "Enfrentamiento directo",
    });
  });

  it("deporte por sets (vóley): 'ganados'/'perdidos', no 'a favor'/'en contra'", () => {
    const etiquetas = etiquetasDesempate({ isPlayedInSets: true, periodLabel: "set" });
    expect(etiquetas.score_difference).toBe("Diferencia de sets");
    expect(etiquetas.score_for).toBe("Sets ganados");
    expect(etiquetas.score_against).toBe("Sets perdidos");
  });

  it("wins y head_to_head son fijos, sin importar el deporte", () => {
    const futbol = etiquetasDesempate({ scoringUnit: "gol" });
    const voley = etiquetasDesempate({ isPlayedInSets: true });
    expect(futbol.wins).toBe(voley.wins);
    expect(futbol.head_to_head).toBe(voley.head_to_head);
  });

  it("sin sportInfo, usa 'puntos' con la forma 'a favor'/'en contra'", () => {
    const etiquetas = etiquetasDesempate(undefined);
    expect(etiquetas.score_for).toBe("Puntos a favor");
    expect(etiquetas.score_against).toBe("Puntos en contra");
  });
});

describe("columnasMarcador", () => {
  it("deporte de suma (fútbol): GF/GC, no una abreviatura genérica", () => {
    expect(columnasMarcador({ scoringUnit: "gol" })).toEqual({ favor: "GF", contra: "GC" });
  });

  it("deporte por sets (vóley): SG/SP -- la abreviatura real, no 'SF'/'SC'", () => {
    expect(columnasMarcador({ isPlayedInSets: true, periodLabel: "set" })).toEqual({ favor: "SG", contra: "SP" });
  });

  it("la inicial sale de unidadDeMarcador, así que respeta el plural irregular de 'gol'", () => {
    // Si en vez de "goles" tomara la letra de "gol" a secas seguiría dando
    // "G" igual -- pero para otra unidad hipotética con inicial distinta en
    // singular/plural, esta prueba es la que lo notaría primero.
    expect(columnasMarcador({ scoringUnit: "gol" }).favor.charAt(0)).toBe("G");
  });

  it("sin sportInfo, usa 'puntos' -> PF/PC", () => {
    expect(columnasMarcador(undefined)).toEqual({ favor: "PF", contra: "PC" });
  });
});

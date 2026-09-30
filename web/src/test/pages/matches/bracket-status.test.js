import { describe, expect, it } from "vitest";
import { quedaRondaPorSortear, rondasDisponibles, motivoDeSorteoInhabilitado } from "src/pages/matches/bracket-status";

function partido(overrides = {}) {
  return { categoryId: "cat1", phase: null, isRepechage: false, roundNumber: null, ...overrides };
}

describe("quedaRondaPorSortear", () => {
  it("false sin categoría elegida", () => {
    expect(quedaRondaPorSortear([partido({ phase: "final" })], null)).toBe(false);
  });

  it("false sin ningún partido de fase (todavía no se sorteó nada de la llave)", () => {
    expect(quedaRondaPorSortear([partido({ phase: null })], "cat1")).toBe(false);
  });

  it("true con partidos de fase pero sin ninguno en la final -- falta sortear al menos otra ronda", () => {
    expect(quedaRondaPorSortear([partido({ phase: "semifinal" })], "cat1")).toBe(true);
  });

  it("false una vez que la final ya está creada -- no hay ronda que sortear", () => {
    expect(quedaRondaPorSortear([partido({ phase: "semifinal" }), partido({ phase: "final" })], "cat1")).toBe(false);
  });

  it("ignora los partidos de repechaje: no cuentan como 'llave sorteada'", () => {
    expect(quedaRondaPorSortear([partido({ phase: "semifinal", isRepechage: true })], "cat1")).toBe(false);
  });

  it("ignora los partidos de otra categoría", () => {
    expect(quedaRondaPorSortear([partido({ phase: "semifinal", categoryId: "otra-categoria" })], "cat1")).toBe(false);
  });
});

describe("rondasDisponibles", () => {
  it("sin categoría elegida, da un arreglo vacío", () => {
    expect(rondasDisponibles([partido({ roundNumber: 1 })], null)).toEqual([]);
  });

  it("solo incluye rondas sin fase (fase de grupos) -- las de eliminatoria reusan números y no cuentan", () => {
    const rondas = rondasDisponibles(
      [partido({ roundNumber: 1, phase: null }), partido({ roundNumber: 1, phase: "final" })],
      "cat1",
    );
    expect(rondas).toEqual([1]);
  });

  it("deduplica y ordena numéricamente ascendente, no alfabéticamente (10 después de 2, no antes)", () => {
    const rondas = rondasDisponibles(
      [partido({ roundNumber: 2 }), partido({ roundNumber: 10 }), partido({ roundNumber: 2 }), partido({ roundNumber: 1 })],
      "cat1",
    );
    expect(rondas).toEqual([1, 2, 10]);
  });

  it("ignora partidos de otra categoría", () => {
    expect(rondasDisponibles([partido({ roundNumber: 1, categoryId: "otra" })], "cat1")).toEqual([]);
  });

  it("un partido sin roundNumber (null) no aparece en la lista", () => {
    expect(rondasDisponibles([partido({ roundNumber: null })], "cat1")).toEqual([]);
  });
});

describe("motivoDeSorteoInhabilitado", () => {
  it("null sin competencia elegida todavía", () => {
    expect(motivoDeSorteoInhabilitado(null, 0)).toBeNull();
  });

  it("null cuando se puede sortear (borrador o programada, sin nada jugado)", () => {
    expect(motivoDeSorteoInhabilitado({ status: "draft" }, 0)).toBeNull();
    expect(motivoDeSorteoInhabilitado({ status: "scheduled" }, 0)).toBeNull();
  });

  it("explica que la competencia ya está en curso cuando el estado no es sorteable", () => {
    expect(motivoDeSorteoInhabilitado({ status: "in_progress" }, 0)).toBe("La competencia ya esta en curso.");
  });

  it("el estado 'en curso' se explica antes que 'ya hay partidos jugados', aunque las dos condiciones se den juntas", () => {
    expect(motivoDeSorteoInhabilitado({ status: "in_progress" }, 5)).toBe("La competencia ya esta en curso.");
  });

  it("explica cuántos partidos ya se jugaron cuando el estado sí sería sorteable", () => {
    expect(motivoDeSorteoInhabilitado({ status: "scheduled" }, 3)).toBe("Ya hay 3 partidos jugados.");
  });
});

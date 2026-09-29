import { describe, expect, it } from "vitest";
import { esIndividual, esJuzgado, registraCantidad, ventanaDeMinuto, aPuntaje, dePuntaje, ADICIONAL_POR_DEFECTO } from "./sport-shape";

describe("esIndividual", () => {
  it("true solo cuando isIndividual es estrictamente true", () => {
    expect(esIndividual({ isIndividual: true })).toBe(true);
  });

  it.each([
    ["isIndividual: false", { isIndividual: false }],
    ["isIndividual: 1 (truthy pero no === true)", { isIndividual: 1 }],
    ["isIndividual ausente", {}],
    ["sportInfo null", null],
    ["sportInfo undefined", undefined],
  ])("false con %s", (_desc, sportInfo) => {
    expect(esIndividual(sportInfo)).toBe(false);
  });
});

describe("esJuzgado", () => {
  it("true solo cuando scoreMode === 'judged'", () => {
    expect(esJuzgado({ scoreMode: "judged" })).toBe(true);
  });

  it.each([
    ["scoreMode cumulative", { scoreMode: "cumulative" }],
    ["scoreMode ausente", {}],
    ["sportInfo null", null],
  ])("false con %s", (_desc, sportInfo) => {
    expect(esJuzgado(sportInfo)).toBe(false);
  });
});

describe("registraCantidad", () => {
  it.each(["football", "futsal", "volleyball"])(
    "false para deportes de a uno (%s): cada evento va por separado, no en lote",
    (code) => {
      expect(registraCantidad({ code })).toBe(false);
    },
  );

  it("true para un deporte no listado como 'de a uno'", () => {
    expect(registraCantidad({ code: "basketball" })).toBe(true);
  });

  it("true cuando no hay sportInfo todavía (sportInfo undefined)", () => {
    expect(registraCantidad(undefined)).toBe(true);
  });
});

describe("ventanaDeMinuto", () => {
  const cumulativo = { scoreMode: "cumulative", periodMinutes: 45 };

  it("el período 1 arranca en el minuto 0", () => {
    expect(ventanaDeMinuto(cumulativo, 1)).toEqual({
      desde: 0,
      regular: 45,
      hasta: 45 + ADICIONAL_POR_DEFECTO,
      adicional: ADICIONAL_POR_DEFECTO,
    });
  });

  it("el período 2 arranca justo después de donde terminó el reglamentario del 1 (el reloj sigue corriendo)", () => {
    // (p-1)*duracion + 1 -- el minuto 46, no el 45, y no reinicia a 0.
    expect(ventanaDeMinuto(cumulativo, 2)).toEqual({
      desde: 46,
      regular: 90,
      hasta: 90 + ADICIONAL_POR_DEFECTO,
      adicional: ADICIONAL_POR_DEFECTO,
    });
  });

  it("usa periodMaxExtraMinutes del reglamento efectivo si viene, en vez del valor por defecto", () => {
    const conAdicionalPropio = { ...cumulativo, periodMaxExtraMinutes: 5 };
    expect(ventanaDeMinuto(conAdicionalPropio, 1)).toEqual({ desde: 0, regular: 45, hasta: 50, adicional: 5 });
  });

  it("periodMaxExtraMinutes: 0 se respeta (no cae al default por ??)", () => {
    const sinAdicional = { ...cumulativo, periodMaxExtraMinutes: 0 };
    expect(ventanaDeMinuto(sinAdicional, 1).adicional).toBe(0);
  });

  it("null si el deporte no es de reloj corrido (scoreMode distinto de 'cumulative')", () => {
    expect(ventanaDeMinuto({ scoreMode: "judged", periodMinutes: 45 }, 1)).toBeNull();
    expect(ventanaDeMinuto({ scoreMode: "sets", periodMinutes: 45 }, 1)).toBeNull();
  });

  it("null si el reglamento no trae periodMinutes", () => {
    expect(ventanaDeMinuto({ scoreMode: "cumulative" }, 1)).toBeNull();
    expect(ventanaDeMinuto({ scoreMode: "cumulative", periodMinutes: 0 }, 1)).toBeNull();
  });

  it("null si todavía no se eligió período (periodo 0, null o undefined)", () => {
    expect(ventanaDeMinuto(cumulativo, 0)).toBeNull();
    expect(ventanaDeMinuto(cumulativo, null)).toBeNull();
    expect(ventanaDeMinuto(cumulativo, undefined)).toBeNull();
  });

  it("null sin sportInfo", () => {
    expect(ventanaDeMinuto(undefined, 1)).toBeNull();
    expect(ventanaDeMinuto(null, 1)).toBeNull();
  });
});

describe("aPuntaje (entero de backend -> texto)", () => {
  it("divide entre 100 y fija dos decimales: 765 -> '7.65'", () => {
    expect(aPuntaje(765)).toBe("7.65");
  });

  it("0 es un puntaje real y se muestra como '0.00', no como vacío", () => {
    expect(aPuntaje(0)).toBe("0.00");
  });

  it("null/undefined (sin puntaje cargado) da cadena vacía, nunca '0.00'", () => {
    expect(aPuntaje(null)).toBe("");
    expect(aPuntaje(undefined)).toBe("");
  });

  it("completa ceros a la derecha cuando hace falta: 700 -> '7.00'", () => {
    expect(aPuntaje(700)).toBe("7.00");
  });
});

describe("dePuntaje (texto de formulario -> entero de backend)", () => {
  it("'7.65' -> 765", () => {
    expect(dePuntaje("7.65")).toBe(765);
  });

  it("Math.round evita el error de punto flotante que un truncado (Math.trunc) sí sufriría", () => {
    // 19.9 * 100 da 1989.9999999999998 en punto flotante -- un truncado se
    // comería ese resto y devolvería 1989 en vez de 1990. Math.round es lo
    // que evita eso (documentado en el propio código de dePuntaje).
    expect(Number("19.9") * 100).not.toBe(1990);
    expect(dePuntaje("19.9")).toBe(1990);
  });

  it("recorta espacios alrededor del número", () => {
    expect(dePuntaje("  7.5  ")).toBe(750);
  });

  it("'0' es un puntaje real: da 0, no null", () => {
    expect(dePuntaje("0")).toBe(0);
  });

  it("vacío, en blanco, null o undefined dan null (nada cargado todavía)", () => {
    expect(dePuntaje("")).toBeNull();
    expect(dePuntaje("   ")).toBeNull();
    expect(dePuntaje(null)).toBeNull();
    expect(dePuntaje(undefined)).toBeNull();
  });

  it("texto no numérico da null en vez de NaN", () => {
    expect(dePuntaje("abc")).toBeNull();
  });

  it("es el inverso exacto de aPuntaje para valores representables con dos decimales", () => {
    expect(dePuntaje(aPuntaje(765))).toBe(765);
    expect(dePuntaje(aPuntaje(0))).toBe(0);
  });
});

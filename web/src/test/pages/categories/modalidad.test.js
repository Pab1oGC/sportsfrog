import { describe, expect, it } from "vitest";
import { MODALIDADES, modalidadDe, modalidadesDisponibles } from "src/pages/categories/modalidad";

describe("modalidadDe", () => {
  it.each([
    ["1", "Individual"],
    ["2", "Pareja"],
    ["3", "Trío"],
  ])("el valor '%s' se lee como '%s'", (value, label) => {
    expect(modalidadDe(value)).toBe(label);
  });

  it("acepta el valor como número, no solo como string (mapToForm puede dejar un número cargado)", () => {
    expect(modalidadDe(2)).toBe("Pareja");
  });

  it("un valor sin modalidad conocida da undefined, no revienta", () => {
    expect(modalidadDe("9")).toBeUndefined();
    expect(modalidadDe("")).toBeUndefined();
    expect(modalidadDe(null)).toBeUndefined();
  });
});

describe("modalidadesDisponibles", () => {
  it("sin techo declarado (null/undefined), ofrece las tres modalidades", () => {
    expect(modalidadesDisponibles(null)).toEqual(MODALIDADES);
    expect(modalidadesDisponibles(undefined)).toEqual(MODALIDADES);
  });

  it("con techo 1 (Kyorugi), solo ofrece Individual -- no hay forma de armar una dupla", () => {
    expect(modalidadesDisponibles(1)).toEqual([{ value: "1", label: "Individual" }]);
  });

  it("con techo 3 (Poomsae), ofrece las tres", () => {
    expect(modalidadesDisponibles(3)).toEqual(MODALIDADES);
  });

  it("con techo 2, ofrece Individual y Pareja, pero no Trío", () => {
    expect(modalidadesDisponibles(2)).toEqual([
      { value: "1", label: "Individual" },
      { value: "2", label: "Pareja" },
    ]);
  });

  it("techo 0 (declarado, no ausente) filtra todas las modalidades -- 0 no es lo mismo que null", () => {
    expect(modalidadesDisponibles(0)).toEqual([]);
  });
});

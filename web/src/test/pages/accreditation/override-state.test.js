import { describe, expect, it } from "vitest";
import { deriveOverrideState, overridesToSend } from "src/pages/accreditation/override-state";

describe("deriveOverrideState", () => {
  it("un elemento del paquete que también aparece en lo resuelto no tiene excepción", () => {
    expect(deriveOverrideState(["a"], ["a"])).toEqual({});
  });

  it("un elemento del paquete que no aparece en lo resuelto fue excluido (granted: false)", () => {
    expect(deriveOverrideState(["a"], [])).toEqual({ a: false });
  });

  it("un elemento fuera del paquete que sí aparece en lo resuelto fue agregado (granted: true)", () => {
    expect(deriveOverrideState([], ["a"])).toEqual({ a: true });
  });

  it("un elemento ausente de ambos no tiene excepción", () => {
    expect(deriveOverrideState(["a"], ["a", "b"])).toEqual({ b: true });
  });

  it("mezcla de los tres casos a la vez, cada uno independiente del resto", () => {
    // "incluido": en el paquete y resuelto -- sin excepción.
    // "excluido": en el paquete, no resuelto -- excepción que quita.
    // "agregado": fuera del paquete, resuelto -- excepción que agrega.
    const estado = deriveOverrideState(
      ["incluido", "excluido"],
      ["incluido", "agregado"],
    );
    expect(estado).toEqual({ excluido: false, agregado: true });
  });

  it("sin paquete ni resuelto, no hay nada que derivar", () => {
    expect(deriveOverrideState([], [])).toEqual({});
  });

  it("acepta null/undefined igual que un arreglo vacío -- categoría sin cargar todavía", () => {
    expect(deriveOverrideState(null, undefined)).toEqual({});
  });
});

describe("overridesToSend", () => {
  it("convierte el mapa itemId->granted al arreglo que espera el servidor", () => {
    expect(overridesToSend({ a: true, b: false })).toEqual([
      { itemId: "a", granted: true },
      { itemId: "b", granted: false },
    ]);
  });

  it('un estado vacío manda un arreglo vacío -- "sin excepciones" es un valor, no una omisión', () => {
    expect(overridesToSend({})).toEqual([]);
  });
});

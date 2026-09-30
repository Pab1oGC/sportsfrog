import { describe, expect, it } from "vitest";
import { estaAbierta, estanTodosPuntuados, hayAlgunoPuntuado } from "src/pages/performances/classification-status";

function fila(status) {
  return { status };
}

describe("estaAbierta", () => {
  it("false con un arreglo vacío -- todavía no se abrió la clasificación", () => {
    expect(estaAbierta([])).toBe(false);
  });

  it("true con al menos una fila, sin importar su estado", () => {
    expect(estaAbierta([fila("pending")])).toBe(true);
  });
});

describe("estanTodosPuntuados", () => {
  it("false si no está abierta (sin filas)", () => {
    expect(estanTodosPuntuados([])).toBe(false);
  });

  it("false si al menos una fila sigue pendiente", () => {
    expect(estanTodosPuntuados([fila("scored"), fila("pending")])).toBe(false);
  });

  it("true solo cuando todas las filas están puntuadas", () => {
    expect(estanTodosPuntuados([fila("scored"), fila("scored")])).toBe(true);
  });
});

describe("hayAlgunoPuntuado", () => {
  it("false sin ninguna fila puntuada", () => {
    expect(hayAlgunoPuntuado([fila("pending"), fila("pending")])).toBe(false);
  });

  it("true con al menos una fila puntuada, aunque no estén todas -- no es lo mismo que estanTodosPuntuados", () => {
    expect(hayAlgunoPuntuado([fila("scored"), fila("pending")])).toBe(true);
  });

  it("false con un arreglo vacío", () => {
    expect(hayAlgunoPuntuado([])).toBe(false);
  });
});

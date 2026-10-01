import { describe, expect, it } from "vitest";
import { gridColumns, gridRows } from "src/pages/lists/list-table";

describe("gridColumns", () => {
  it("una columna por entrada, con field posicional (c0, c1, ...)", () => {
    const columns = gridColumns([{ header: "Nombre", kind: "text" }, { header: "Goles", kind: "number" }]);

    expect(columns.map((c) => c.field)).toEqual(["c0", "c1"]);
    expect(columns.map((c) => c.headerName)).toEqual(["Nombre", "Goles"]);
  });

  it("una columna numérica se alinea a la derecha, el resto a la izquierda", () => {
    const [nombre, goles] = gridColumns([{ header: "Nombre", kind: "text" }, { header: "Goles", kind: "number" }]);

    expect(nombre.align).toBe("left");
    expect(goles.align).toBe("right");
    expect(goles.headerAlign).toBe("right");
  });

  it("sin columnas (o undefined) no rompe", () => {
    expect(gridColumns([])).toEqual([]);
    expect(gridColumns(undefined)).toEqual([]);
  });
});

describe("gridRows", () => {
  it("cada fila-arreglo se vuelve un objeto por columna, con un id posicional", () => {
    const rows = gridRows([["Diaz", 4], ["Soto", 2]]);

    expect(rows).toEqual([
      { id: 0, c0: "Diaz", c1: 4 },
      { id: 1, c0: "Soto", c1: 2 },
    ]);
  });

  it("sin filas (o undefined) no rompe", () => {
    expect(gridRows([])).toEqual([]);
    expect(gridRows(undefined)).toEqual([]);
  });
});

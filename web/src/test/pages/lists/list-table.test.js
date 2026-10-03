import { describe, expect, it } from "vitest";
import { alignmentFor, gridColumns, gridRows } from "src/pages/lists/list-table";

describe("alignmentFor", () => {
  it("un numero que no es la primera columna se alinea a la derecha", () => {
    expect(alignmentFor("number", false)).toBe("right");
  });

  // La excepcion: todo proveedor que pone un numero en la primera columna lo
  // usa como puesto, nunca como cantidad -- alineado a la derecha queda
  // pegado al borde interno con puro espacio vacio adelante en cada fila,
  // que es el "ocupa mucho espacio hacia su izquierda" que esto corrige.
  it("un numero que SI es la primera columna se centra, no se alinea a la derecha", () => {
    expect(alignmentFor("number", true)).toBe("center");
  });

  it("un booleano siempre se centra, sea o no la primera columna", () => {
    expect(alignmentFor("boolean", false)).toBe("center");
    expect(alignmentFor("boolean", true)).toBe("center");
  });

  it("texto, fecha y fecha-hora siempre a la izquierda, sea o no la primera columna", () => {
    for (const kind of ["text", "date", "date_time"]) {
      expect(alignmentFor(kind, false)).toBe("left");
      expect(alignmentFor(kind, true)).toBe("left");
    }
  });
});

describe("gridColumns", () => {
  it("una columna por entrada, con field posicional (c0, c1, ...)", () => {
    const columns = gridColumns([{ header: "Nombre", kind: "text" }, { header: "Goles", kind: "number" }]);

    expect(columns.map((c) => c.field)).toEqual(["c0", "c1"]);
    expect(columns.map((c) => c.headerName)).toEqual(["Nombre", "Goles"]);
  });

  it("una columna numérica se alinea a la derecha (encabezado y dato por igual), el resto a la izquierda", () => {
    const [nombre, goles] = gridColumns([{ header: "Nombre", kind: "text" }, { header: "Goles", kind: "number" }]);

    expect(nombre.align).toBe("left");
    expect(nombre.headerAlign).toBe("left");
    expect(goles.align).toBe("right");
    expect(goles.headerAlign).toBe("right");
  });

  it("una columna numérica que es la PRIMERA se centra en vez de alinearse a la derecha", () => {
    const [posicion, equipo] = gridColumns([{ header: "Pos.", kind: "number" }, { header: "Equipo", kind: "text" }]);

    expect(posicion.align).toBe("center");
    expect(posicion.headerAlign).toBe("center");
    expect(equipo.align).toBe("left");
  });

  it("una columna booleana se centra (encabezado y dato por igual)", () => {
    const [activo] = gridColumns([{ header: "Activo", kind: "boolean" }]);

    expect(activo.align).toBe("center");
    expect(activo.headerAlign).toBe("center");
  });

  it("una columna corta (numero, booleano, fecha) pide un ancho fijo en vez de repartirse el espacio por igual", () => {
    const [numero, booleano, fecha, fechaHora] = gridColumns([
      { header: "Pos.", kind: "number" },
      { header: "Activo", kind: "boolean" },
      { header: "Nacimiento", kind: "date" },
      { header: "Registrado", kind: "date_time" },
    ]);

    expect(numero.width).toBeGreaterThan(0);
    expect(numero.flex).toBeUndefined();
    expect(booleano.width).toBeGreaterThan(0);
    expect(fecha.width).toBeGreaterThan(0);
    expect(fechaHora.width).toBeGreaterThan(0);
  });

  it("solo el texto se reparte el espacio sobrante (flex), para no quedar tan angosto como una columna corta", () => {
    const [nombre] = gridColumns([{ header: "Nombre", kind: "text" }]);

    expect(nombre.flex).toBe(1);
    expect(nombre.width).toBeUndefined();
  });

  it("una columna de puesto nunca queda tan ancha como una de texto libre", () => {
    const [posicion, equipo] = gridColumns([{ header: "Pos.", kind: "number" }, { header: "Equipo", kind: "text" }]);

    // "Pos." pide un ancho fijo y chico; "Equipo" se reparte lo que sobra --
    // la misma desproporción que antes hacia que la primera columna
    // (siempre una corta: un puesto, un codigo) saliera enorme.
    expect(posicion.width).toBeLessThan(120);
    expect(equipo.flex).toBeGreaterThan(0);
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

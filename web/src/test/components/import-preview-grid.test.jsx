import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ImportPreviewGrid } from "src/components/import-preview-grid";

function fila(overrides = {}) {
  return { number: 1, document: "12345678", name: "Juan Pérez", outcome: "register", problems: [], ...overrides };
}

describe("ImportPreviewGrid -- mapeo de outcome a chip", () => {
  it.each([
    ["register", "Registrar"],
    ["create_and_register", "Crear+Registrar"],
    ["already_registered", "Ya registrado"],
    ["rejected", "Rechazado"],
  ])("outcome '%s' se muestra como '%s'", (outcome, etiqueta) => {
    render(<ImportPreviewGrid rows={[fila({ outcome })]} />);
    expect(screen.getByText(etiqueta)).toBeInTheDocument();
  });

  it("un outcome desconocido muestra el código crudo en vez de esconder la fila", () => {
    render(<ImportPreviewGrid rows={[fila({ outcome: "codigo_nuevo_sin_mapear" })]} />);
    expect(screen.getByText("codigo_nuevo_sin_mapear")).toBeInTheDocument();
  });
});

describe("ImportPreviewGrid -- observaciones", () => {
  it("sin problemas, muestra '--'", () => {
    render(<ImportPreviewGrid rows={[fila({ problems: [] })]} />);
    expect(screen.getByText("--")).toBeInTheDocument();
  });

  it("con problemas, muestra cada uno en su propia línea, no recortado", () => {
    render(<ImportPreviewGrid rows={[fila({ problems: ["Documento duplicado en la planilla", "Categoría no existe en esta competencia"] })]} />);
    expect(screen.getByText("Documento duplicado en la planilla")).toBeInTheDocument();
    expect(screen.getByText("Categoría no existe en esta competencia")).toBeInTheDocument();
  });
});

describe("ImportPreviewGrid -- columna de categoría", () => {
  it("conCategoria=false (por defecto) no muestra la columna Categoria", () => {
    render(<ImportPreviewGrid rows={[fila()]} />);
    expect(screen.queryByText("Categoria")).not.toBeInTheDocument();
  });

  it("conCategoria=true muestra la columna, con '--' si la fila no trae una", () => {
    render(<ImportPreviewGrid rows={[fila({ category: null })]} conCategoria />);
    expect(screen.getByText("Categoria")).toBeInTheDocument();
    // Dos veces: una por la categoría vacía, otra por "sin problemas" -- las
    // dos columnas usan el mismo placeholder.
    expect(screen.getAllByText("--")).toHaveLength(2);
  });

  it("conCategoria=true muestra el valor de la fila cuando lo trae", () => {
    render(<ImportPreviewGrid rows={[fila({ category: "Sub-15" })]} conCategoria />);
    expect(screen.getByText("Sub-15")).toBeInTheDocument();
  });
});

describe("ImportPreviewGrid -- filas", () => {
  it("sin rows (undefined), no revienta y no muestra filas", () => {
    expect(() => render(<ImportPreviewGrid />)).not.toThrow();
  });

  it("cada fila muestra su nombre y documento", () => {
    render(<ImportPreviewGrid rows={[fila({ name: "María Gómez", document: "87654321" })]} />);
    expect(screen.getByText("María Gómez")).toBeInTheDocument();
    expect(screen.getByText("87654321")).toBeInTheDocument();
  });
});

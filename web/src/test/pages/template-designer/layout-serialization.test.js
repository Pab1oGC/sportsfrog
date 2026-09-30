import { describe, expect, it } from "vitest";
import { PROPIEDADES, caraVacia, limpiarCampo, limpiarCara } from "src/pages/template-designer/layout-serialization";

describe("caraVacia", () => {
  it("sin proporción declarada, usa 1.5875 (la de una credencial estándar)", () => {
    expect(caraVacia()).toEqual({ backgroundKey: null, aspectRatio: 1.5875, fields: [] });
  });

  it("respeta la proporción que se le pasa", () => {
    expect(caraVacia(1.4).aspectRatio).toBe(1.4);
  });
});

describe("limpiarCampo", () => {
  it("conserva solo las propiedades que TemplateField conoce", () => {
    const campo = { source: "text", x: 0.1, y: 0.2, propiedadInventada: "esto no debería sobrevivir" };
    expect(limpiarCampo(campo)).toEqual({ source: "text", x: 0.1, y: 0.2 });
  });

  it("no inventa propiedades que el campo no traía", () => {
    const limpio = limpiarCampo({ source: "text" });
    expect(Object.keys(limpio)).toEqual(["source"]);
  });

  it("conserva un valor 0 o false -- no los trata como ausentes", () => {
    const limpio = limpiarCampo({ source: "text", x: 0, bold: false });
    expect(limpio).toEqual({ source: "text", x: 0, bold: false });
  });

  it("las propiedades conocidas son exactamente las que declara TemplateField", () => {
    expect(PROPIEDADES).toEqual(["source", "x", "y", "w", "h", "size", "font", "align", "fit", "minSize", "color", "bold", "text"]);
  });
});

describe("limpiarCara", () => {
  it("null da null -- una cara sin reverso no se inventa una vacía", () => {
    expect(limpiarCara(null)).toBeNull();
  });

  it("limpia cada campo de la lista con limpiarCampo", () => {
    const cara = { backgroundKey: "k1", aspectRatio: 1.4, fields: [{ source: "text", propiedadInventada: "x" }] };
    expect(limpiarCara(cara)).toEqual({
      backgroundKey: "k1",
      aspectRatio: 1.4,
      fields: [{ source: "text" }],
    });
  });

  it("sin aspectRatio declarado, cae en 1.5875", () => {
    expect(limpiarCara({ fields: [] }).aspectRatio).toBe(1.5875);
  });

  it("sin backgroundKey, cae en null (no undefined ni cadena vacía)", () => {
    expect(limpiarCara({ fields: [] }).backgroundKey).toBeNull();
  });

  it("sin fields, da un arreglo vacío", () => {
    expect(limpiarCara({}).fields).toEqual([]);
  });
});

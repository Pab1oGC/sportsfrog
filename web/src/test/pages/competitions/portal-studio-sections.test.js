import { describe, expect, it } from "vitest";
import { seccionesVisibles, ordenTrasMover } from "src/pages/competitions/portal-studio-sections";

function seccion(key, label = key) {
  return { key, label };
}

describe("seccionesVisibles", () => {
  const ordenBase = [seccion("standings"), seccion("leaders"), seccion("classification"), seccion("calendar"), seccion("gallery")];

  it("sin form todavía (null), da un arreglo vacío", () => {
    expect(seccionesVisibles(null)).toEqual([]);
  });

  it("calendar siempre está visible, sin interruptor que lo apague", () => {
    const form = { showStandings: false, showLeaders: false, showClassification: false, showGallery: false, gallery: [], sectionOrder: ordenBase };
    expect(seccionesVisibles(form).map((s) => s.key)).toEqual(["calendar"]);
  });

  it("standings/leaders/classification siguen su propio interruptor", () => {
    const form = { showStandings: true, showLeaders: false, showClassification: true, showGallery: false, gallery: [], sectionOrder: ordenBase };
    expect(seccionesVisibles(form).map((s) => s.key)).toEqual(["standings", "classification", "calendar"]);
  });

  it("gallery necesita el interruptor prendido Y al menos una foto -- ninguna de las dos sola alcanza", () => {
    const conInterruptorSinFotos = { showStandings: false, showLeaders: false, showClassification: false, showGallery: true, gallery: [], sectionOrder: ordenBase };
    expect(seccionesVisibles(conInterruptorSinFotos).map((s) => s.key)).not.toContain("gallery");

    const conFotosSinInterruptor = { showStandings: false, showLeaders: false, showClassification: false, showGallery: false, gallery: [{ key: "g1" }], sectionOrder: ordenBase };
    expect(seccionesVisibles(conFotosSinInterruptor).map((s) => s.key)).not.toContain("gallery");

    const conLosDos = { showStandings: false, showLeaders: false, showClassification: false, showGallery: true, gallery: [{ key: "g1" }], sectionOrder: ordenBase };
    expect(seccionesVisibles(conLosDos).map((s) => s.key)).toContain("gallery");
  });

  it("respeta el orden de sectionOrder, no un orden fijo propio", () => {
    const ordenInvertido = [...ordenBase].reverse();
    const form = { showStandings: true, showLeaders: true, showClassification: true, showGallery: false, gallery: [], sectionOrder: ordenInvertido };
    expect(seccionesVisibles(form).map((s) => s.key)).toEqual(["calendar", "classification", "leaders", "standings"]);
  });

  it("bracket no está en la lista de interruptores -- si aparece en sectionOrder sin entrada en 'visible', queda afuera", () => {
    const conBracket = [...ordenBase, seccion("bracket")];
    const form = { showStandings: false, showLeaders: false, showClassification: false, showGallery: false, gallery: [], sectionOrder: conBracket };
    expect(seccionesVisibles(form).map((s) => s.key)).not.toContain("bracket");
  });
});

describe("ordenTrasMover", () => {
  const orden = [seccion("a"), seccion("b"), seccion("c")];

  it("mover hacia abajo (direction +1) intercambia con el siguiente", () => {
    const next = ordenTrasMover(orden, 0, 1);
    expect(next.map((s) => s.key)).toEqual(["b", "a", "c"]);
  });

  it("mover hacia arriba (direction -1) intercambia con el anterior", () => {
    const next = ordenTrasMover(orden, 2, -1);
    expect(next.map((s) => s.key)).toEqual(["a", "c", "b"]);
  });

  it("mover la primera fila hacia arriba no hace nada -- devuelve la MISMA referencia", () => {
    const next = ordenTrasMover(orden, 0, -1);
    expect(next).toBe(orden);
  });

  it("mover la última fila hacia abajo tampoco hace nada", () => {
    const next = ordenTrasMover(orden, 2, 1);
    expect(next).toBe(orden);
  });

  it("no muta el arreglo original", () => {
    const copia = [...orden];
    ordenTrasMover(orden, 0, 1);
    expect(orden).toEqual(copia);
  });
});

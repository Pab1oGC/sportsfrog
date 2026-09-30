import { describe, expect, it } from "vitest";
import { puntoFocalDesdeClic } from "src/pages/competitions/focal-point";

function rect({ left = 0, top = 0, width = 200, height = 100 } = {}) {
  return { left, top, width, height };
}

describe("puntoFocalDesdeClic", () => {
  it("un clic en la esquina superior izquierda da 0,0", () => {
    expect(puntoFocalDesdeClic(rect(), 0, 0)).toEqual({ x: 0, y: 0 });
  });

  it("un clic en el centro da 50,50", () => {
    expect(puntoFocalDesdeClic(rect(), 100, 50)).toEqual({ x: 50, y: 50 });
  });

  it("un clic en la esquina inferior derecha da 100,100", () => {
    expect(puntoFocalDesdeClic(rect(), 200, 100)).toEqual({ x: 100, y: 100 });
  });

  it("redondea al entero más cercano", () => {
    expect(puntoFocalDesdeClic(rect({ width: 300 }), 100, 0).x).toBe(33); // 33.33... -> 33
  });

  it("recorta a 0..100 aunque el clic (o el cálculo) caiga fuera del rectángulo", () => {
    expect(puntoFocalDesdeClic(rect(), -50, -50)).toEqual({ x: 0, y: 0 });
    expect(puntoFocalDesdeClic(rect(), 500, 500)).toEqual({ x: 100, y: 100 });
  });

  it("tiene en cuenta el offset del rectángulo (left/top), no coordenadas absolutas de la ventana", () => {
    expect(puntoFocalDesdeClic(rect({ left: 50, top: 20, width: 200, height: 100 }), 150, 70)).toEqual({ x: 50, y: 50 });
  });
});

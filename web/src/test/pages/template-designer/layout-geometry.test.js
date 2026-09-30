import { describe, expect, it } from "vitest";
import { MINIMO, TAMANO_MAXIMO, limitar, cajaDe, cambiosDesdeCaja, calcular } from "src/pages/template-designer/layout-geometry";

describe("limitar", () => {
  it("deja pasar un valor ya dentro del rango", () => {
    expect(limitar(0.5, 0, 1)).toBe(0.5);
  });

  it("recorta hacia el mínimo", () => {
    expect(limitar(-1, 0, 1)).toBe(0);
  });

  it("recorta hacia el máximo", () => {
    expect(limitar(2, 0, 1)).toBe(1);
  });
});

describe("cajaDe", () => {
  it("una imagen usa 'h' directamente, con 0.1 por defecto si no lo trae", () => {
    expect(cajaDe({ x: 0.1, y: 0.2, w: 0.3 }, true)).toEqual({ x: 0.1, y: 0.2, w: 0.3, h: 0.1 });
    expect(cajaDe({ x: 0.1, y: 0.2, w: 0.3, h: 0.4 }, true)).toEqual({ x: 0.1, y: 0.2, w: 0.3, h: 0.4 });
  });

  it("un texto usa 'size' como alto, con 0.06 por defecto", () => {
    expect(cajaDe({ x: 0, y: 0 }, false)).toMatchObject({ h: 0.06 });
    expect(cajaDe({ x: 0, y: 0, size: 0.1 }, false)).toMatchObject({ h: 0.1 });
  });

  it("sin w declarado, se extiende hasta el borde derecho (1 - x)", () => {
    expect(cajaDe({ x: 0.3 }, false).w).toBeCloseTo(0.7);
  });

  it("x/y ausentes caen en 0", () => {
    expect(cajaDe({}, true)).toMatchObject({ x: 0, y: 0 });
  });
});

describe("cambiosDesdeCaja", () => {
  it("una imagen escribe x/y/w/h tal cual, sin tocar minSize", () => {
    const cambios = cambiosDesdeCaja({ x: 0.1, y: 0.2, w: 0.3, h: 0.4 }, true, { minSize: 0.9 });
    expect(cambios).toEqual({ x: 0.1, y: 0.2, w: 0.3, h: 0.4 });
  });

  it("un texto escribe 'size' (no 'h'), acotado a TAMANO_MAXIMO", () => {
    const cambios = cambiosDesdeCaja({ x: 0, y: 0, w: 1, h: 0.8 }, false, null);
    expect(cambios.size).toBe(TAMANO_MAXIMO);
    expect(cambios).not.toHaveProperty("h");
  });

  it("un texto dentro del máximo conserva su tamaño exacto", () => {
    const cambios = cambiosDesdeCaja({ x: 0, y: 0, w: 1, h: 0.2 }, false, null);
    expect(cambios.size).toBe(0.2);
  });

  it("si el minSize actual queda por encima del nuevo tamaño, se lo baja junto con size", () => {
    const cambios = cambiosDesdeCaja({ x: 0, y: 0, w: 1, h: 0.1 }, false, { minSize: 0.3 });
    expect(cambios.size).toBe(0.1);
    expect(cambios.minSize).toBe(0.1);
  });

  it("si el minSize actual ya es menor o igual, no lo toca (no aparece en el resultado)", () => {
    const cambios = cambiosDesdeCaja({ x: 0, y: 0, w: 1, h: 0.3 }, false, { minSize: 0.1 });
    expect(cambios).not.toHaveProperty("minSize");
  });

  it("sin campoActual (null), no revienta -- simplemente no hay minSize que ajustar", () => {
    expect(() => cambiosDesdeCaja({ x: 0, y: 0, w: 1, h: 0.1 }, false, null)).not.toThrow();
  });
});

describe("calcular -- arrastrar el cuerpo (sin asa)", () => {
  const caja = { x: 0.3, y: 0.3, w: 0.2, h: 0.1 };

  it("mueve x/y por dx/dy, sin tocar w/h", () => {
    const r = calcular({ caja, asa: null, imagen: false }, 0.1, -0.1);
    expect(r.x).toBeCloseTo(0.4);
    expect(r.y).toBeCloseTo(0.2);
    expect(r.w).toBe(0.2);
    expect(r.h).toBe(0.1);
  });

  it("no deja que el borde derecho salga de 1 (x se recorta a 1 - w)", () => {
    const r = calcular({ caja, asa: null, imagen: false }, 10, 0);
    expect(r.x).toBeCloseTo(0.8); // 1 - 0.2
  });

  it("no deja que x baje de 0", () => {
    const r = calcular({ caja, asa: null, imagen: false }, -10, 0);
    expect(r.x).toBe(0);
  });

  it("mismo recorte en el eje Y, con el alto de la caja", () => {
    const r = calcular({ caja, asa: null, imagen: false }, 0, 10);
    expect(r.y).toBeCloseTo(0.9); // 1 - 0.1
  });
});

describe("calcular -- asa izquierda (oeste)", () => {
  const asaOeste = { izq: true, der: false, arr: false, aba: false };
  const caja = { x: 0.3, y: 0.3, w: 0.3, h: 0.1 };

  it("mover el borde izquierdo hacia la derecha achica el ancho y corre x", () => {
    const r = calcular({ caja, asa: asaOeste, imagen: false }, 0.1, 0);
    expect(r.x).toBeCloseTo(0.4);
    expect(r.w).toBeCloseTo(0.2);
  });

  it("mover el borde izquierdo hacia la izquierda agranda el ancho", () => {
    const r = calcular({ caja, asa: asaOeste, imagen: false }, -0.1, 0);
    expect(r.x).toBeCloseTo(0.2);
    expect(r.w).toBeCloseTo(0.4);
  });

  it("no deja que el ancho baje de MINIMO", () => {
    const r = calcular({ caja, asa: asaOeste, imagen: false }, 10, 0);
    expect(r.w).toBeCloseTo(MINIMO);
  });

  it("no deja que x baje de 0", () => {
    const r = calcular({ caja, asa: asaOeste, imagen: false }, -10, 0);
    expect(r.x).toBe(0);
  });

  it("no toca y/h", () => {
    const r = calcular({ caja, asa: asaOeste, imagen: false }, 0.1, 0);
    expect(r.y).toBe(0.3);
    expect(r.h).toBe(0.1);
  });
});

describe("calcular -- asa derecha (este)", () => {
  const asaEste = { izq: false, der: true, arr: false, aba: false };
  const caja = { x: 0.3, y: 0.3, w: 0.3, h: 0.1 };

  it("mover el borde derecho hacia afuera agranda el ancho, sin tocar x", () => {
    const r = calcular({ caja, asa: asaEste, imagen: false }, 0.1, 0);
    expect(r.w).toBeCloseTo(0.4);
    expect(r.x).toBe(0.3);
  });

  it("mover el borde derecho hacia adentro achica el ancho", () => {
    const r = calcular({ caja, asa: asaEste, imagen: false }, -0.1, 0);
    expect(r.w).toBeCloseTo(0.2);
  });

  it("no deja que el ancho baje de MINIMO", () => {
    const r = calcular({ caja, asa: asaEste, imagen: false }, -10, 0);
    expect(r.w).toBeCloseTo(MINIMO);
  });

  it("no deja que el borde derecho salga de 1 (w se recorta a 1 - x)", () => {
    const r = calcular({ caja, asa: asaEste, imagen: false }, 10, 0);
    expect(r.w).toBeCloseTo(0.7); // 1 - 0.3
  });
});

describe("calcular -- asa arriba (norte)", () => {
  const asaNorte = { izq: false, der: false, arr: true, aba: false };
  const caja = { x: 0.3, y: 0.3, w: 0.2, h: 0.1 };

  it("mover el borde superior hacia arriba agranda el alto y sube y, manteniendo fijo el borde inferior", () => {
    const r = calcular({ caja, asa: asaNorte, imagen: false }, 0, -0.1);
    expect(r.h).toBeCloseTo(0.2);
    expect(r.y).toBeCloseTo(0.2);
    expect(r.y + r.h).toBeCloseTo(caja.y + caja.h); // el borde de abajo no se mueve
  });

  it("mover el borde superior hacia abajo achica el alto", () => {
    const r = calcular({ caja, asa: asaNorte, imagen: false }, 0, 0.05);
    expect(r.h).toBeCloseTo(0.05);
  });

  it("no deja que el alto baje de MINIMO", () => {
    const r = calcular({ caja, asa: asaNorte, imagen: false }, 0, 10);
    expect(r.h).toBeCloseTo(MINIMO);
  });

  it("un texto no puede crecer más allá de TAMANO_MAXIMO, aunque el lienzo dejara más lugar", () => {
    // Una caja chica cerca del borde inferior: el lienzo dejaría crecer casi
    // hasta 0.95 (el borde superior solo puede llegar hasta 0), pero el
    // techo de un texto es 0.5 -- ese es el límite que de verdad aplica acá.
    const cajaCercaDelBorde = { x: 0.3, y: 0.9, w: 0.2, h: 0.05 };
    const r = calcular({ caja: cajaCercaDelBorde, asa: asaNorte, imagen: false }, 0, -10);
    expect(r.h).toBeCloseTo(TAMANO_MAXIMO);
  });

  it("una imagen no tiene ese techo de 0.5 -- en la misma situación llega hasta donde el lienzo lo permite", () => {
    const cajaCercaDelBorde = { x: 0.3, y: 0.9, w: 0.2, h: 0.05 };
    const r = calcular({ caja: cajaCercaDelBorde, asa: asaNorte, imagen: true }, 0, -10);
    expect(r.h).toBeCloseTo(0.95); // 0.9 + 0.05 - 0, el borde superior tocando el lienzo
  });
});

describe("calcular -- asa abajo (sur)", () => {
  const asaSur = { izq: false, der: false, arr: false, aba: true };
  const caja = { x: 0.3, y: 0.3, w: 0.2, h: 0.1 };

  it("mover el borde inferior hacia abajo agranda el alto, sin tocar y", () => {
    const r = calcular({ caja, asa: asaSur, imagen: false }, 0, 0.1);
    expect(r.h).toBeCloseTo(0.2);
    expect(r.y).toBe(0.3);
  });

  it("un texto no puede crecer más allá de TAMANO_MAXIMO", () => {
    const r = calcular({ caja, asa: asaSur, imagen: false }, 0, 10);
    expect(r.h).toBeCloseTo(TAMANO_MAXIMO);
  });

  it("tampoco puede salirse del lienzo por abajo (h recortado a 1 - y)", () => {
    // y=0.7 deja como máximo 0.3 de alto antes de salirse -- menor que TAMANO_MAXIMO.
    const r = calcular({ caja: { ...caja, y: 0.7 }, asa: asaSur, imagen: false }, 0, 10);
    expect(r.h).toBeCloseTo(0.3);
  });

  it("no deja que el alto baje de MINIMO", () => {
    const r = calcular({ caja, asa: asaSur, imagen: false }, 0, -10);
    expect(r.h).toBeCloseTo(MINIMO);
  });
});

describe("calcular -- una esquina combina dos asas a la vez", () => {
  it("noroeste (izq + arr) cambia x/w y también y/h en el mismo gesto", () => {
    const asaNoroeste = { izq: true, der: false, arr: true, aba: false };
    const caja = { x: 0.3, y: 0.3, w: 0.3, h: 0.2 };
    const r = calcular({ caja, asa: asaNoroeste, imagen: false }, -0.05, -0.05);
    expect(r.x).toBeCloseTo(0.25);
    expect(r.w).toBeCloseTo(0.35);
    expect(r.y).toBeCloseTo(0.25);
    expect(r.h).toBeCloseTo(0.25);
  });

  it("sureste (der + aba) agranda ancho y alto a la vez, sin mover x/y", () => {
    const asaSureste = { izq: false, der: true, arr: false, aba: true };
    const caja = { x: 0.3, y: 0.3, w: 0.3, h: 0.2 };
    const r = calcular({ caja, asa: asaSureste, imagen: false }, 0.1, 0.1);
    expect(r.w).toBeCloseTo(0.4);
    expect(r.h).toBeCloseTo(0.3);
    expect(r.x).toBe(0.3);
    expect(r.y).toBe(0.3);
  });
});

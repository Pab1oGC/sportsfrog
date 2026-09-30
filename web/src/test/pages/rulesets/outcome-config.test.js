import { describe, expect, it } from "vitest";
import { desenlacesDeSets, etiquetaDesenlace, actualizarConfig } from "src/pages/rulesets/outcome-config";

describe("desenlacesDeSets", () => {
  it("mejor-de-3 (toWin=2) da los cuatro desenlaces posibles, ganados y perdidos, desde 0", () => {
    expect(desenlacesDeSets(3)).toEqual(["win_2_0", "loss_0_2", "win_2_1", "loss_1_2"]);
  });

  it("mejor-de-5 (toWin=3) da seis desenlaces", () => {
    expect(desenlacesDeSets(5)).toEqual(["win_3_0", "loss_0_3", "win_3_1", "loss_1_3", "win_3_2", "loss_2_3"]);
  });

  it("un solo set (toWin=1) da un único desenlace posible por lado", () => {
    expect(desenlacesDeSets(1)).toEqual(["win_1_0", "loss_0_1"]);
  });

  it("sin count todavía (0, null, undefined) cae en el default de 1 -- nunca revienta con un bucle vacío raro", () => {
    expect(desenlacesDeSets(0)).toEqual(["win_1_0", "loss_0_1"]);
    expect(desenlacesDeSets(null)).toEqual(["win_1_0", "loss_0_1"]);
    expect(desenlacesDeSets(undefined)).toEqual(["win_1_0", "loss_0_1"]);
  });

  it("un count par redondea hacia arriba para el toWin (mejor-de-4 se gana en 2, igual que mejor-de-3)", () => {
    expect(desenlacesDeSets(4)).toEqual(desenlacesDeSets(3));
  });
});

describe("etiquetaDesenlace", () => {
  it("los tres códigos fijos de un deporte de suma tienen su propia etiqueta", () => {
    expect(etiquetaDesenlace("win")).toBe("Pts victoria");
    expect(etiquetaDesenlace("draw")).toBe("Pts empate");
    expect(etiquetaDesenlace("loss")).toBe("Pts derrota");
  });

  it("un código de sets 'win_N_M' se lee como 'Pts N-M (ganado)'", () => {
    expect(etiquetaDesenlace("win_2_0")).toBe("Pts 2-0 (ganado)");
  });

  it("un código de sets 'loss_M_N' se lee como 'Pts M-N (perdido)'", () => {
    expect(etiquetaDesenlace("loss_0_2")).toBe("Pts 0-2 (perdido)");
  });

  it("un código que no matchea ningún patrón conocido se muestra tal cual, no oculta la fila", () => {
    expect(etiquetaDesenlace("codigo-futuro")).toBe("codigo-futuro");
  });
});

describe("actualizarConfig", () => {
  it("una ruta de un solo nivel actualiza esa clave, sin tocar el resto del config", () => {
    const original = { points: { win: 3 }, walkover: null };
    const resultado = actualizarConfig(original, "walkover", { winnerScore: 3, loserScore: 0 });
    expect(resultado).toEqual({ points: { win: 3 }, walkover: { winnerScore: 3, loserScore: 0 } });
  });

  it("no muta el config original -- devuelve un objeto nuevo", () => {
    const original = { points: {}, walkover: null };
    const resultado = actualizarConfig(original, "walkover", { winnerScore: 3, loserScore: 0 });
    expect(original.walkover).toBeNull();
    expect(resultado).not.toBe(original);
  });

  it("DEFECTO CONOCIDO: una ruta de dos niveles ('points.<code>') no aplica el cambio -- se pierde en silencio", () => {
    // Documentado en el comentario del propio módulo: el bucle copia el
    // nivel intermedio a una variable local sin volver a conectarlo con el
    // config que se devuelve. Esta es la única ruta de dos niveles que usa
    // rulesets-page.jsx en producción (el puntaje por desenlace) -- ese
    // campo hoy no puede editarse. Se fija el comportamiento actual, no se
    // corrige acá.
    const original = { points: { win: 3, loss: 0 } };
    const resultado = actualizarConfig(original, "points.win", 999);
    expect(resultado.points.win).toBe(3); // sigue en 3, el 999 tipeado se perdió
    expect(resultado).toEqual(original);
  });

  it("una ruta de un nivel para 'points' entero (reemplazar todo el objeto) sí funciona -- no pasa por el bucle", () => {
    const original = { points: { win: 3 } };
    const resultado = actualizarConfig(original, "points", { win: 5, loss: 1 });
    expect(resultado.points).toEqual({ win: 5, loss: 1 });
  });
});

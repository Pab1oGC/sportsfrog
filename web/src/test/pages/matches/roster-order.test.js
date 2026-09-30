import { describe, expect, it } from "vitest";
import { porPosicionYDorsal } from "src/pages/matches/roster-order";

function jugador(overrides = {}) {
  return { id: "j", position: null, jerseyNumber: null, ...overrides };
}

function ordenar(jugadores) {
  return jugadores.slice().sort(porPosicionYDorsal).map((j) => j.id);
}

describe("porPosicionYDorsal", () => {
  it("ordena por posición alfabéticamente primero", () => {
    const orden = ordenar([jugador({ id: "portero", position: "Portero" }), jugador({ id: "defensor", position: "Defensor" })]);
    expect(orden).toEqual(["defensor", "portero"]);
  });

  it("dentro de la misma posición, desempata por dorsal ascendente", () => {
    const orden = ordenar([
      jugador({ id: "10", position: "Delantero", jerseyNumber: 10 }),
      jugador({ id: "9", position: "Delantero", jerseyNumber: 9 }),
    ]);
    expect(orden).toEqual(["9", "10"]);
  });

  it("sin posición cargada, va al final -- no se mezcla con los que sí la tienen", () => {
    const orden = ordenar([
      jugador({ id: "sin-posicion", position: null, jerseyNumber: 1 }),
      jugador({ id: "con-posicion", position: "Arquero", jerseyNumber: 99 }),
    ]);
    expect(orden).toEqual(["con-posicion", "sin-posicion"]);
  });

  it("sin dorsal, se ordena al final dentro de su propia posición (999 implícito)", () => {
    const orden = ordenar([
      jugador({ id: "sin-dorsal", position: "Defensor", jerseyNumber: null }),
      jugador({ id: "con-dorsal", position: "Defensor", jerseyNumber: 4 }),
    ]);
    expect(orden).toEqual(["con-dorsal", "sin-dorsal"]);
  });

  it("dorsal 0 es un dorsal real, no 'sin dorsal' -- ordena antes que cualquiera con número", () => {
    const orden = ordenar([
      jugador({ id: "num-5", position: "Defensor", jerseyNumber: 5 }),
      jugador({ id: "num-0", position: "Defensor", jerseyNumber: 0 }),
    ]);
    expect(orden).toEqual(["num-0", "num-5"]);
  });
});

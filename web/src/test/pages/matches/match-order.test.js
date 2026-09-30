import { describe, expect, it } from "vitest";
import { compararPartidos } from "src/pages/matches/match-order";

/** Un partido mínimo, ya "aumentado" con groupLabel como lo entrega matches-page.jsx antes de ordenar. */
function partido(overrides = {}) {
  return {
    id: "m",
    status: "scheduled",
    phase: null,
    roundNumber: null,
    groupLabel: null,
    ...overrides,
  };
}

/** Ordena una lista con el comparador y devuelve solo los ids, para leer el resultado de un vistazo. */
function ordenar(partidos) {
  return partidos.slice().sort(compararPartidos).map((m) => m.id);
}

describe("compararPartidos -- nivel 1: pendiente vs decidido", () => {
  it("lo pendiente (scheduled/in_progress/postponed) siempre va antes que lo decidido", () => {
    const orden = ordenar([
      partido({ id: "finalizado", status: "finished" }),
      partido({ id: "cancelado", status: "cancelled" }),
      partido({ id: "en-curso", status: "in_progress" }),
      partido({ id: "programado", status: "scheduled" }),
      partido({ id: "walkover", status: "walkover" }),
      partido({ id: "aplazado", status: "postponed" }),
    ]);

    // Los tres pendientes (en el orden que sea entre ellos) van antes que
    // los tres decididos.
    const posicionDeLosPendientes = ["en-curso", "programado", "aplazado"].map((id) => orden.indexOf(id));
    const posicionDeLosDecididos = ["finalizado", "cancelado", "walkover"].map((id) => orden.indexOf(id));
    expect(Math.max(...posicionDeLosPendientes)).toBeLessThan(Math.min(...posicionDeLosDecididos));
  });

  it("in_progress cuenta como pendiente, no como decidido", () => {
    const orden = ordenar([partido({ id: "terminado", status: "finished" }), partido({ id: "jugandose", status: "in_progress" })]);
    expect(orden).toEqual(["jugandose", "terminado"]);
  });
});

describe("compararPartidos -- nivel 2/3: fase vs jornada de grupos, con el signo invertido entre pendientes y decididos", () => {
  it("entre pendientes, jornada de grupos va antes que fase de eliminatoria (se juega primero)", () => {
    const orden = ordenar([
      partido({ id: "cruce", status: "scheduled", phase: "final", roundNumber: 1 }),
      partido({ id: "jornada", status: "scheduled", phase: null, roundNumber: 1 }),
    ]);
    expect(orden).toEqual(["jornada", "cruce"]);
  });

  it("entre decididos, la eliminatoria va antes que los grupos ya jugados (orden invertido)", () => {
    const orden = ordenar([
      partido({ id: "grupo-jugado", status: "finished", phase: null, roundNumber: 1 }),
      partido({ id: "cruce-jugado", status: "finished", phase: "final", roundNumber: 1 }),
    ]);
    expect(orden).toEqual(["cruce-jugado", "grupo-jugado"]);
  });
});

describe("compararPartidos -- nivel 4: orden de ronda, también invertido entre pendientes y decididos", () => {
  it("entre pendientes de grupos, la jornada más baja va primero", () => {
    const orden = ordenar([
      partido({ id: "jornada-3", status: "scheduled", roundNumber: 3 }),
      partido({ id: "jornada-1", status: "scheduled", roundNumber: 1 }),
      partido({ id: "jornada-2", status: "scheduled", roundNumber: 2 }),
    ]);
    expect(orden).toEqual(["jornada-1", "jornada-2", "jornada-3"]);
  });

  it("entre decididos de grupos, la jornada más reciente (más alta) va primero", () => {
    const orden = ordenar([
      partido({ id: "jornada-1", status: "finished", roundNumber: 1 }),
      partido({ id: "jornada-3", status: "finished", roundNumber: 3 }),
      partido({ id: "jornada-2", status: "finished", roundNumber: 2 }),
    ]);
    expect(orden).toEqual(["jornada-3", "jornada-2", "jornada-1"]);
  });

  it("entre pendientes de eliminatoria, la ronda más baja va primero (cuartos antes que semifinal)", () => {
    const orden = ordenar([
      partido({ id: "semifinal", status: "scheduled", phase: "semifinal", roundNumber: 2 }),
      partido({ id: "cuartos", status: "scheduled", phase: "cuartos", roundNumber: 1 }),
    ]);
    expect(orden).toEqual(["cuartos", "semifinal"]);
  });

  it("entre decididos de eliminatoria, la ronda más alta (la más jugada recientemente) va primero", () => {
    const orden = ordenar([
      partido({ id: "cuartos", status: "finished", phase: "cuartos", roundNumber: 1 }),
      partido({ id: "semifinal", status: "finished", phase: "semifinal", roundNumber: 2 }),
    ]);
    expect(orden).toEqual(["semifinal", "cuartos"]);
  });
});

describe("compararPartidos -- desempate por grupo (solo dentro de la fase de grupos)", () => {
  it("misma jornada, distinto grupo: desempata por groupLabel alfabético, sin importar pendiente/decidido", () => {
    const orden = ordenar([
      partido({ id: "grupo-b", status: "scheduled", roundNumber: 1, groupLabel: "B" }),
      partido({ id: "grupo-a", status: "scheduled", roundNumber: 1, groupLabel: "A" }),
    ]);
    expect(orden).toEqual(["grupo-a", "grupo-b"]);
  });

  it("un groupLabel ausente (null) se trata como cadena vacía, sin reventar", () => {
    expect(() =>
      ordenar([partido({ id: "sin-grupo", status: "scheduled", roundNumber: 1, groupLabel: null }), partido({ id: "con-grupo", status: "scheduled", roundNumber: 1, groupLabel: "A" })]),
    ).not.toThrow();
  });

  it("en fase de eliminatoria, el groupLabel heredado del equipo NO desempata -- solo importa la ronda", () => {
    // Un cruce trae el groupLabel del equipo local (que sigue siendo el de su
    // grupo de origen), pero acá ya no debe usarse para ordenar.
    const orden = ordenar([
      partido({ id: "cruce-b", status: "scheduled", phase: "semifinal", roundNumber: 1, groupLabel: "B" }),
      partido({ id: "cruce-a", status: "scheduled", phase: "semifinal", roundNumber: 1, groupLabel: "A" }),
    ]);
    // Misma ronda y fase: el orden entre ellos queda como estaban (Array.sort
    // es estable), no alfabético por grupo -- "cruce-b" se mantiene primero.
    expect(orden).toEqual(["cruce-b", "cruce-a"]);
  });
});

describe("compararPartidos -- roundNumber ausente", () => {
  it("roundNumber null/undefined se trata como 0 en la resta, sin reventar", () => {
    const orden = ordenar([
      partido({ id: "con-ronda", status: "scheduled", roundNumber: 2 }),
      partido({ id: "sin-ronda", status: "scheduled", roundNumber: null }),
    ]);
    expect(orden).toEqual(["sin-ronda", "con-ronda"]);
  });
});

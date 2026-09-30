import { describe, expect, it } from "vitest";
import { mapaDeGrupoPorEquipo } from "src/pages/matches/team-groups";

describe("mapaDeGrupoPorEquipo", () => {
  it("indexa el groupLabel de cada equipo por su id", () => {
    const mapa = mapaDeGrupoPorEquipo([
      { id: "t1", groupLabel: "A" },
      { id: "t2", groupLabel: "B" },
    ]);
    expect(mapa).toEqual({ t1: "A", t2: "B" });
  });

  it("sin equipos (undefined o []) da un mapa vacío, no revienta", () => {
    expect(mapaDeGrupoPorEquipo(undefined)).toEqual({});
    expect(mapaDeGrupoPorEquipo([])).toEqual({});
  });

  it("un equipo sin groupLabel (fase de eliminatoria) queda con undefined, no se omite del mapa", () => {
    const mapa = mapaDeGrupoPorEquipo([{ id: "t1", groupLabel: null }]);
    expect(mapa).toHaveProperty("t1", null);
  });
});

import { describe, expect, it } from "vitest";
import { endpoints } from "src/lib/axios";
import { crearUrlDeAlta, mapearEnvioDeNomina } from "src/pages/roster/registration-mapping";

describe("crearUrlDeAlta", () => {
  it("sin equipo elegido todavía, da null -- no hay a dónde mandar el alta", () => {
    expect(crearUrlDeAlta(null, { athleteIds: ["a1"] })).toBeNull();
  });

  it("un solo deportista elegido va al alta individual (RegisterPlayer)", () => {
    expect(crearUrlDeAlta("t1", { athleteIds: ["a1"] })).toBe(endpoints.roster("t1"));
  });

  it("más de un deportista elegido va al alta en bloque (RegisterPlayersBulk)", () => {
    expect(crearUrlDeAlta("t1", { athleteIds: ["a1", "a2"] })).toBe(endpoints.rosterRegisterBulk("t1"));
  });

  it("ningún deportista elegido todavía (arreglo vacío) sigue yendo al endpoint individual", () => {
    expect(crearUrlDeAlta("t1", { athleteIds: [] })).toBe(endpoints.roster("t1"));
  });
});

describe("mapearEnvioDeNomina", () => {
  it("al editar, manda solo dorsal y posición -- CorrectRegistration no acepta athleteId", () => {
    const cuerpo = mapearEnvioDeNomina({ athleteIds: ["a1"], jerseyNumber: "10", position: "Defensor" }, true);
    expect(cuerpo).toEqual({ jerseyNumber: 10, position: "Defensor" });
    expect(cuerpo).not.toHaveProperty("athleteId");
    expect(cuerpo).not.toHaveProperty("athleteIds");
  });

  it("dorsal y posición vacíos van como null al editar, no como cadena vacía o NaN", () => {
    const cuerpo = mapearEnvioDeNomina({ athleteIds: [], jerseyNumber: "", position: "" }, true);
    expect(cuerpo).toEqual({ jerseyNumber: null, position: null });
  });

  it("al dar de alta a varios de una vez, manda solo los ids -- sin dorsal ni posición, no hay uno solo que pedir", () => {
    const cuerpo = mapearEnvioDeNomina({ athleteIds: ["a1", "a2"], jerseyNumber: "10", position: "Defensor" }, false);
    expect(cuerpo).toEqual({ athleteIds: ["a1", "a2"] });
  });

  it("al dar de alta a uno solo, manda athleteId suelto junto con dorsal y posición", () => {
    const cuerpo = mapearEnvioDeNomina({ athleteIds: ["a1"], jerseyNumber: "7", position: "Arquero" }, false);
    expect(cuerpo).toEqual({ athleteId: "a1", jerseyNumber: 7, position: "Arquero" });
  });

  it("dorsal y posición vacíos también van como null al dar de alta a uno solo", () => {
    const cuerpo = mapearEnvioDeNomina({ athleteIds: ["a1"], jerseyNumber: "", position: "" }, false);
    expect(cuerpo).toEqual({ athleteId: "a1", jerseyNumber: null, position: null });
  });

  it("dorsal '0' es un dorsal real y se manda como 0, no como null", () => {
    const cuerpo = mapearEnvioDeNomina({ athleteIds: ["a1"], jerseyNumber: "0", position: "" }, false);
    expect(cuerpo.jerseyNumber).toBe(0);
  });
});

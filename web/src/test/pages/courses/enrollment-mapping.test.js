import { describe, expect, it } from "vitest";
import { endpoints } from "src/lib/axios";
import { crearUrlDeInscripcion, mapearEnvioDeInscripcion } from "src/pages/courses/enrollment-mapping";

describe("crearUrlDeInscripcion", () => {
  it("sin curso elegido todavía, da null -- no hay a dónde mandar la inscripción", () => {
    expect(crearUrlDeInscripcion(null, { athleteIds: ["a1"] })).toBeNull();
  });

  it("un solo estudiante elegido va al alta individual (EnrollStudent)", () => {
    expect(crearUrlDeInscripcion("c1", { athleteIds: ["a1"] })).toBe(endpoints.courseEnrollments("c1"));
  });

  it("más de un estudiante elegido va al alta en bloque (EnrollStudentsBulk)", () => {
    expect(crearUrlDeInscripcion("c1", { athleteIds: ["a1", "a2"] })).toBe(endpoints.courseEnrollmentsBulk("c1"));
  });

  it("ningún estudiante elegido todavía (arreglo vacío) sigue yendo al endpoint individual", () => {
    expect(crearUrlDeInscripcion("c1", { athleteIds: [] })).toBe(endpoints.courseEnrollments("c1"));
  });
});

describe("mapearEnvioDeInscripcion", () => {
  it("al inscribir a varios de una vez, manda solo los ids", () => {
    expect(mapearEnvioDeInscripcion({ athleteIds: ["a1", "a2", "a3"] })).toEqual({ athleteIds: ["a1", "a2", "a3"] });
  });

  it("al inscribir a uno solo, manda athleteId suelto", () => {
    expect(mapearEnvioDeInscripcion({ athleteIds: ["a1"] })).toEqual({ athleteId: "a1" });
  });
});

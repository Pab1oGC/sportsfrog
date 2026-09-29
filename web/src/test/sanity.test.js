import { describe, expect, it } from "vitest";

// Prueba trivial del Bloque 0: si esto no corre en verde, el problema está
// en vitest.config.js/setup.js, no en ningún módulo de la aplicación.
describe("infraestructura de pruebas", () => {
  it("corre pruebas de vitest", () => {
    expect(1 + 1).toBe(2);
  });
});

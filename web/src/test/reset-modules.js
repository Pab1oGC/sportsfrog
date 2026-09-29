import { vi } from "vitest";

/**
 * enMemoria (auth/context/jwt/session-store.js) y renovacionEnCurso
 * (lib/axios.js) son variables de módulo, no exportadas. Vitest cachea un
 * módulo la primera vez que algo lo importa en un archivo de pruebas, así
 * que sin esto la segunda prueba del archivo hereda el resto de sesión o la
 * renovación en vuelo que dejó la anterior.
 *
 * `vi.resetModules()` vacía el registro de módulos de Vitest, pero solo
 * afecta a los `import()` dinámicos que se hagan *después* de llamarla -- un
 * `import` estático de arriba del archivo ya quedó atado al primer registro
 * y no ve el reset. Por eso el patrón en un archivo que dependa de este
 * estado es:
 *
 *   beforeEach(async () => {
 *     resetModules();
 *     ({ guardarRenovacion, leerRenovacion } = await import("src/auth/context/jwt/session-store"));
 *   });
 *
 * en vez de un `import { guardarRenovacion } from "..."` estático.
 */
export function resetModules() {
  vi.resetModules();
}

import { defineConfig, mergeConfig } from "vitest/config";
import viteConfig from "./vite.config.js";

// mergeConfig reutiliza el alias "src" de vite.config.js en vez de
// redeclararlo acá: es exactamente el objeto que ya normaliza separadores
// con .split(path.sep).join("/") (ver el comentario ahí) para que Windows no
// le dé dos identidades de módulo al mismo archivo según se lo importe por
// alias o por ruta relativa. Redeclarar el alias a mano en este archivo es
// la manera más fácil de que ese arreglo se desincronice sin que nadie lo note.
export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: "jsdom",
      setupFiles: ["./src/test/setup.js"],
      // e2e/ son specs de Playwright, no de Vitest -- el patrón de test por
      // defecto de Vitest (**/*.spec.*) los encuentra igual y truena porque
      // ese archivo llama test() de @playwright/test fuera de un test run de
      // Playwright.
      exclude: ["**/node_modules/**", "**/dist/**", "e2e/**"],
      // Zona horaria fija: lib/format-date.js y el cálculo de utcOffsetMinutes
      // de matches-page.jsx leen la zona del proceso. Sin esto, una prueba de
      // fechas pasa en una máquina y falla en otra (o en CI). Buenos Aires no
      // tiene horario de verano, así que además es una zona estable para todo
      // lo que no está probando específicamente el cambio de horario -- lo
      // que sí lo necesita (Bloque 5, generarJornada) fija otra zona con
      // vi.stubEnv("TZ", ...) puntualmente en esa prueba.
      env: { TZ: "America/Argentina/Buenos_Aires" },
      css: true,
      restoreMocks: true,
    },
  }),
);

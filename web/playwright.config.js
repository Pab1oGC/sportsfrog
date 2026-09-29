import { defineConfig, devices } from "@playwright/test";

// Bloque 7 del plan de pruebas: todo lo que necesita un navegador real
// (arrastre con PointerEvent, ResizeObserver + getBoundingClientRect,
// Leaflet, descargas) con la red interceptada por page.route() -- sin
// Docker, sin backend levantado. webServer levanta el propio Vite dev server
// y Playwright espera a que responda antes de correr una sola prueba.
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: "html",
  use: {
    baseURL: "http://localhost:5173",
    trace: "on-first-retry",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
  ],
  webServer: {
    command: "pnpm dev",
    url: "http://localhost:5173",
    reuseExistingServer: !process.env.CI,
    timeout: 30_000,
  },
});

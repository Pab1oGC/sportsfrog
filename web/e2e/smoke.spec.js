import { test, expect } from "@playwright/test";

// Prueba trivial del Bloque 0 para el corredor de Playwright: si esto no
// carga, el problema está en playwright.config.js o en que el dev server no
// levantó -- no en ninguna prueba de extremo a extremo real del Bloque 7.
test("la landing carga y muestra el título de la app", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveTitle("SportFrog");
  await expect(page.getByText("SportFrog", { exact: true }).first()).toBeVisible();
});

import { test, expect } from "@playwright/test";

/**
 * Bloque 7, item 3: los conectores de la llave dependen de medidas reales
 * del layout (`getBoundingClientRect`, `ResizeObserver`, dos
 * `requestAnimationFrame` encadenados) -- en jsdom todo mide cero, asi que
 * esto solo se puede probar en un navegador real.
 */

const ORG = "liga-test";
const COMP = "torneo-bracket";

function compFixture() {
  return {
    name: "Copa Bracket",
    season: "2026",
    organizationName: "Liga Test",
    sportName: "Futbol",
    status: "in_progress",
    format: "knockout",
    isJudged: false,
    isIndividual: false,
    portal: { theme: null, gallery: [] },
    shows: { standings: false, leaders: false, classification: false, gallery: false, bracket: true, rosters: false },
  };
}

function partido(overrides) {
  return {
    id: "m",
    categoryId: "cat1",
    categoryName: "Sub-15",
    phase: null,
    roundNumber: 1,
    status: "finished",
    homeTeamName: "A",
    awayTeamName: "B",
    homeTotal: 1,
    awayTotal: 0,
    scheduledAt: null,
    venueName: null,
    spaceName: null,
    ...overrides,
  };
}

test("las lineas del conector se calculan y dibujan en un navegador real", async ({ page }) => {
  await page.route(`**/api/public/${ORG}/${COMP}`, async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(compFixture()) });
  });

  await page.route(`**/api/public/${ORG}/${COMP}/matches`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        fixtures: [
          // Ronda 1: Equipo A gana, Equipo D gana -- los dos avanzan a la
          // semifinal, que por eso tiene dos padres (uno por cada entrante).
          partido({ id: "q1", phase: "quarterfinal", roundNumber: 1, homeTeamName: "Equipo A", awayTeamName: "Equipo B", homeTotal: 2, awayTotal: 1 }),
          partido({ id: "q2", phase: "quarterfinal", roundNumber: 1, homeTeamName: "Equipo C", awayTeamName: "Equipo D", homeTotal: 0, awayTotal: 2 }),
          partido({ id: "s1", phase: "semifinal", roundNumber: 2, status: "scheduled", homeTeamName: "Equipo A", awayTeamName: "Equipo D", homeTotal: null, awayTotal: null }),
        ],
      }),
    });
  });

  await page.goto(`/public/${ORG}/${COMP}`);
  await page.getByRole("tab", { name: "Llave" }).click();

  await expect(page.getByText("Equipo A").first()).toBeVisible();
  await expect(page.getByText("Equipo D").first()).toBeVisible();

  // BracketConnectors mide despues del ResizeObserver y dos rAF encadenados
  // -- esperar dos frames reales del navegador es mas confiable que un
  // temporizador fijo, y es justo lo que el propio componente hace.
  await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

  // Sin data-testid, "svg[aria-hidden='true']" tambien atrapa cada icono de
  // Iconify de la pagina (todos decorativos, todos aria-hidden) -- de ahi el
  // atributo agregado en el propio componente, solo para esto.
  const lineas = page.getByTestId("bracket-connectors").locator("path");
  await expect.poll(() => lineas.count()).toBeGreaterThan(0);

  // La semifinal tiene dos padres reales (Equipo A de q1, Equipo D de q2):
  // dos lineas convergiendo en la misma caja, ninguna hacia Equipo B ni
  // Equipo C -- esos quedaron eliminados y no aparecen en la ronda 2.
  expect(await lineas.count()).toBe(2);
});

import { test, expect } from "@playwright/test";

/**
 * Bloque 7, item 7: el portal publico, de punta a punta, con la red
 * interceptada. La matriz de visibilidad en si (que seccion se ve segun
 * `shows`) ya esta cubierta a nivel de componente en el Bloque 6 -- lo que
 * agrega un navegador real aca es lo que ese nivel no puede ver: que el
 * boton de cronologia efectivamente desaparece de TODAS las tarjetas
 * renderizadas (no solo de una tarjeta de prueba aislada), y que la pestaña
 * "Llave" no exista en absoluto en el DOM, no solo que este oculta.
 */

const ORG = "liga-test";
const COMP = "torneo-test";
const COMP_URL = `**/api/public/${ORG}/${COMP}`;
const MATCHES_URL = `**/api/public/${ORG}/${COMP}/matches`;

function compFixture(overrides = {}) {
  return {
    name: "Copa Apertura",
    season: "2026",
    organizationName: "Liga Test",
    sportName: "Futbol",
    status: "in_progress",
    format: "groups",
    isJudged: false,
    isIndividual: false,
    portal: { theme: null, gallery: [] },
    shows: { standings: true, leaders: true, classification: false, gallery: false, bracket: false, rosters: false },
    ...overrides,
  };
}

function partido(overrides = {}) {
  return {
    id: "m1",
    categoryId: "cat1",
    categoryName: "Sub-15",
    phase: null,
    roundNumber: 1,
    status: "in_progress",
    homeTeamName: "Local",
    awayTeamName: "Visitante",
    homeTotal: 1,
    awayTotal: 0,
    scheduledAt: null,
    venueName: null,
    spaceName: null,
    ...overrides,
  };
}

async function mockCompetition(page, comp) {
  await page.route(COMP_URL, async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(comp) });
  });
}

async function mockMatches(page, fixtures) {
  await page.route(MATCHES_URL, async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ fixtures }) });
  });
}

test.describe("'no publicada' es indistinguible de 'inexistente'", () => {
  const casos = [
    { nombre: "slug inexistente", status: 404 },
    { nombre: "slug real pero sin publicar", status: 404 },
    { nombre: "el servidor responde con error", status: 500 },
  ];

  for (const caso of casos) {
    test(`${caso.nombre} -> el mismo mensaje unico`, async ({ page }) => {
      await page.route(COMP_URL, async (route) => {
        await route.fulfill({ status: caso.status, contentType: "application/json", body: JSON.stringify({ detail: "no importa el detalle" }) });
      });

      await page.goto(`/public/${ORG}/${COMP}`);

      await expect(page.getByText("Competencia no encontrada o no publicada.")).toBeVisible();
    });
  }
});

test("shows.rosters === false quita el boton de cronologia de TODAS las tarjetas de partido", async ({ page }) => {
  await mockCompetition(page, compFixture({ shows: { standings: false, leaders: false, classification: false, gallery: false, bracket: false, rosters: false } }));
  await mockMatches(page, [
    partido({ id: "m1", status: "in_progress", homeTeamName: "Deportivo Norte", awayTeamName: "Atletico Sur" }),
    partido({ id: "m2", status: "finished", homeTeamName: "Union Central", awayTeamName: "Estrella Roja", homeTotal: 2, awayTotal: 1 }),
  ]);

  await page.goto(`/public/${ORG}/${COMP}`);

  // La unica seccion visible es Calendario (todo lo demas esta apagado en el
  // fixture), asi que ya arranca en la pestaña que muestra las tarjetas.
  await expect(page.getByText("Deportivo Norte")).toBeVisible();
  await expect(page.getByText("Union Central")).toBeVisible();
  await expect(page.getByRole("button", { name: "Cronologia" })).toHaveCount(0);
});

test("shows.rosters === true muestra el boton de cronologia en las tarjetas de partidos en curso o finalizados", async ({ page }) => {
  await mockCompetition(page, compFixture({ shows: { standings: false, leaders: false, classification: false, gallery: false, bracket: false, rosters: true } }));
  await mockMatches(page, [
    partido({ id: "m1", status: "in_progress", homeTeamName: "Deportivo Norte", awayTeamName: "Atletico Sur" }),
    partido({ id: "m2", status: "scheduled", homeTeamName: "Sin Empezar A", awayTeamName: "Sin Empezar B", homeTotal: null, awayTotal: null }),
  ]);

  await page.goto(`/public/${ORG}/${COMP}`);

  await expect(page.getByText("Deportivo Norte")).toBeVisible();
  // Un partido todavia no empezado no tiene cronologia que mostrar, aunque
  // shows.rosters este activo -- puedaVerCronologia tambien exige el status.
  await expect(page.getByRole("button", { name: "Cronologia" })).toHaveCount(1);
});

test("shows.bracket === false: la pestaña 'Llave' no existe en absoluto", async ({ page }) => {
  await mockCompetition(page, compFixture({ shows: { standings: true, leaders: false, classification: false, gallery: false, bracket: false, rosters: false } }));
  await mockMatches(page, []);

  await page.goto(`/public/${ORG}/${COMP}`);

  // shows.standings sigue activo -- la pagina renderiza igual, solo que sin
  // el Tab de "Llave" entre los que arma.
  await expect(page.getByRole("tab").first()).toBeVisible();
  await expect(page.getByRole("tab", { name: "Llave" })).toHaveCount(0);
});

test("shows.bracket === true: la pestaña 'Llave' existe y se puede abrir", async ({ page }) => {
  await mockCompetition(page, compFixture({ shows: { standings: false, leaders: false, classification: false, gallery: false, bracket: true, rosters: false } }));
  await mockMatches(page, [
    partido({ id: "q1", phase: "quarterfinal", roundNumber: 1, status: "finished", homeTeamName: "Equipo A", awayTeamName: "Equipo B", homeTotal: 2, awayTotal: 1 }),
  ]);

  await page.goto(`/public/${ORG}/${COMP}`);

  const tabLlave = page.getByRole("tab", { name: "Llave" });
  await expect(tabLlave).toBeVisible();
  await tabLlave.click();
  // "Equipo A" aparece dos veces (la tarjeta del cruce y el panel lateral de
  // equipos participantes) -- alcanza con que alguna sea visible.
  await expect(page.getByText("Equipo A").first()).toBeVisible();
});

import { test, expect } from "@playwright/test";
import { loginAs } from "./helpers/auth";

/**
 * Bloque 7, item 6: `LocationPicker` combina Leaflet real (nada que jsdom
 * pueda montar) con las tres ramas del efecto de `[value]` -- coordenadas
 * directas, enlace corto que hay que resolver contra el servidor, y enlace
 * no reconocido -- mas la busqueda por direccion contra Nominatim.
 */

async function abrirSelector(page) {
  await loginAs(page);
  // Sin comodin al final: "**/api/venues**" tambien atraparia
  // /api/venues/resolve-maps-link (Playwright prueba las rutas registradas
  // mas tarde primero), tapando el mock especifico que cada prueba registra
  // para esa otra ruta.
  await page.route("**/api/venues", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
  });
  // Las loza de OpenStreetMap se sirven de verdad si no se bloquean --
  // innecesario para lo que esta prueba verifica, y el propio plan pide
  // que el Bloque 7 sea determinista y no dependa de la red real.
  await page.route("**tile.openstreetmap.org/**", (route) => route.abort());

  await page.goto("/dashboard/venues");
  await page.getByRole("button", { name: "Nueva sede" }).click();
  return page.getByLabel("Enlace de Google Maps");
}

test("un enlace con coordenadas directas ('q=lat,lng') se reconoce sin pedir nada al servidor", async ({ page }) => {
  let resolverLlamado = false;
  await page.route("**/api/venues/resolve-maps-link**", async (route) => {
    resolverLlamado = true;
    await route.fulfill({ status: 200, contentType: "application/json", body: "{}" });
  });

  const campoEnlace = await abrirSelector(page);
  await campoEnlace.fill("https://www.google.com/maps?q=-17.783000,-63.182000");

  await expect(page.getByText("Hacé click en el mapa o arrastrá el marcador para ajustar el punto exacto.")).toBeVisible();
  expect(resolverLlamado).toBe(false);
});

test("un enlace corto (maps.app.goo.gl) se resuelve contra el servidor y reescribe el campo de texto", async ({ page }) => {
  await page.route("**/api/venues/resolve-maps-link**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ resolvedUrl: "https://www.google.com/maps/@-17.783000,-63.182000,15z" }),
    });
  });

  const campoEnlace = await abrirSelector(page);
  await campoEnlace.fill("https://maps.app.goo.gl/AbCdEf123");

  await expect(page.getByText("Hacé click en el mapa o arrastrá el marcador para ajustar el punto exacto.")).toBeVisible();
  // El enlace corto original se reemplaza por uno normal con las
  // coordenadas ya resueltas -- lo que se guarda no puede seguir siendo un
  // enlace que el navegador de quien lo mire despues no pueda seguir.
  await expect(campoEnlace).toHaveValue("https://www.google.com/maps?q=-17.783000,-63.182000");
});

test("un enlace no reconocido (ni coordenadas ni host de enlace corto) avisa y NO llama al servidor", async ({ page }) => {
  let resolverLlamado = false;
  await page.route("**/api/venues/resolve-maps-link**", async (route) => {
    resolverLlamado = true;
    await route.fulfill({ status: 200, contentType: "application/json", body: "{}" });
  });

  const campoEnlace = await abrirSelector(page);
  await campoEnlace.fill("https://www.google.com/maps/place/Plaza+Principal/");

  await expect(page.getByText(/No pudimos ubicar el punto exacto de ese enlace en el mapa/)).toBeVisible();
  expect(resolverLlamado).toBe(false);
  // El enlace pegado se conserva igual -- el aviso lo dice explicitamente.
  await expect(campoEnlace).toHaveValue("https://www.google.com/maps/place/Plaza+Principal/");
});

test("buscar una direccion contra Nominatim completa el enlace de Google Maps", async ({ page }) => {
  await page.route("https://nominatim.openstreetmap.org/search**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([{ lat: "-17.783000", lon: "-63.182000", display_name: "Plaza Principal, Bolivia" }]),
    });
  });

  await abrirSelector(page);
  await page.getByLabel("Buscar dirección").fill("Plaza Principal");
  await page.getByRole("button", { name: "Buscar" }).click();

  await expect(page.getByLabel("Enlace de Google Maps")).toHaveValue("https://www.google.com/maps?q=-17.783000,-63.182000");
});

test("una direccion sin resultados en Nominatim muestra el error, sin tocar el enlace", async ({ page }) => {
  await page.route("https://nominatim.openstreetmap.org/search**", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
  });

  await abrirSelector(page);
  await page.getByLabel("Buscar dirección").fill("Un lugar que no existe en ningun lado");
  await page.getByRole("button", { name: "Buscar" }).click();

  await expect(page.getByText("No se encontró esa dirección.")).toBeVisible();
  await expect(page.getByLabel("Enlace de Google Maps")).toHaveValue("");
});

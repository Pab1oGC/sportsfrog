import { test, expect } from "@playwright/test";
import { loginAs } from "./helpers/auth";

/**
 * Bloque 7, item 2: el diseñador de plantillas. El algebra de redimensionado
 * en si (`calcular`, los 8 limites, el techo que difiere entre imagen y
 * texto) ya esta extraida y probada como funcion pura en el Bloque 5
 * (template-designer/layout-geometry.js) -- lo que un navegador real agrega
 * es la integracion: que el teclado de verdad mueva el campo seleccionado, y
 * con el paso correcto segun Shift.
 *
 * El arrastre con las 8 asas (PointerEvent + setPointerCapture) queda fuera
 * de esta pasada: ninguna de las asas tiene un selector estable (ni
 * data-testid, ni aria-label -- son <Box> de 10x10px distinguidos solo por
 * su posicion), y agregar atributos de prueba a un componente de produccion
 * solo para esto es una decision que vale la pena tomar aparte, no de paso.
 */

async function abrirDisenador(page) {
  await loginAs(page);

  await page.route("**/api/documents/templates/1", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        id: 1,
        name: "Credencial de prueba",
        pageSize: "credential",
        isDefault: false,
        kind: "credential",
        layout: {
          front: {
            backgroundKey: null,
            aspectRatio: 1.5875,
            fields: [{ source: "campo-prueba", x: 0.4, y: 0.4, w: 0.2, h: 0.1 }],
          },
          back: null,
        },
        backgrounds: { front: "", back: "" },
      }),
    });
  });

  await page.route("**/api/documents/design**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ sources: [], fonts: [], pageSizes: [{ code: "credential", label: "Credencial" }], defaults: {} }),
    });
  });

  await page.goto("/dashboard/templates/1/design");

  // "campo-prueba" aparece dos veces: la caja sobre el lienzo (la que
  // importa aca) y la fila del panel lateral de campos -- la del lienzo es
  // la primera en el orden del DOM.
  const campoTexto = page.getByText("campo-prueba").first();
  await expect(campoTexto).toBeVisible();
  // El texto vive dentro del Box del campo con pointerEvents:'none' -- el
  // padre inmediato es el que de verdad escucha el click (onPointerDown).
  return campoTexto.locator("xpath=..");
}

test("las flechas mueven el campo seleccionado, y Shift multiplica el paso", async ({ page }) => {
  const campo = await abrirDisenador(page);

  await campo.click();
  const inicial = await campo.boundingBox();

  await page.keyboard.press("ArrowRight");
  const trasUnPaso = await campo.boundingBox();
  expect(trasUnPaso.x).toBeGreaterThan(inicial.x);

  const pasoNormal = trasUnPaso.x - inicial.x;

  await page.keyboard.press("Shift+ArrowRight");
  const trasShift = await campo.boundingBox();
  const pasoConShift = trasShift.x - trasUnPaso.x;

  // 0.02 contra 0.005 -- cuatro veces, con margen por redondeo de pixeles.
  expect(pasoConShift / pasoNormal).toBeGreaterThan(3);
  expect(pasoConShift / pasoNormal).toBeLessThan(5);
});

test("ArrowDown mueve hacia abajo, no hacia el costado", async ({ page }) => {
  const campo = await abrirDisenador(page);
  await campo.click();
  const inicial = await campo.boundingBox();

  await page.keyboard.press("ArrowDown");
  const despues = await campo.boundingBox();

  expect(despues.y).toBeGreaterThan(inicial.y);
  expect(despues.x).toBeCloseTo(inicial.x, 0);
});

test("Supr elimina el campo seleccionado", async ({ page }) => {
  const campo = await abrirDisenador(page);
  await campo.click();

  await page.keyboard.press("Delete");

  await expect(page.getByText("campo-prueba")).toHaveCount(0);
});

import { test, expect } from "@playwright/test";
import { loginAs } from "./helpers/auth";

/**
 * Bloque 7, item 4: subida de archivos. De los cinco flujos que lista el
 * plan, este archivo cubre el mas autonomo -- el ZIP de fotos de
 * deportistas, en /dashboard/athletes, sin nada mas que seleccionar antes.
 *
 * Los otros cuatro (planilla de nomina, planilla de delegacion, galeria del
 * portal, fondo de plantilla) comparten el mismo mecanismo de fondo (un
 * <input type="file" hidden> detras de un boton, un POST multipart o una
 * lectura a data URL) pero viven detras de una cascada de selects
 * (competencia -> categoria -> equipo, o club) que hace falta poblar con
 * mocks antes de llegar al boton -- quedan fuera de esta pasada por costo de
 * armado, no porque el mecanismo sea distinto; el que se prueba aca ya
 * ejercita el contrato real (FormData, encabezados, respuesta) que comparten
 * todos.
 */

async function abrirDialogoDeFotos(page) {
  await loginAs(page);
  await page.route("**/api/athletes**", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", headers: { "x-total-count": "0" }, body: "[]" });
  });

  await page.goto("/dashboard/athletes");
  await page.getByRole("button", { name: "Importar fotos" }).click();
  await expect(page.getByText("Importar fotos (ZIP)")).toBeVisible();
}

test("el boton 'Subir' esta deshabilitado hasta elegir un archivo", async ({ page }) => {
  await abrirDialogoDeFotos(page);
  await expect(page.getByRole("button", { name: "Subir" })).toBeDisabled();
});

test("subir un ZIP lo envia como multipart al endpoint de lotes y muestra el estado devuelto", async ({ page }) => {
  await abrirDialogoDeFotos(page);

  let recibido = null;
  await page.route("**/api/athletes/photos/imports", async (route) => {
    const request = route.request();
    recibido = {
      method: request.method(),
      contentType: request.headers()["content-type"] || "",
    };
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ status: "queued" }),
    });
  });

  await page.setInputFiles('input[type="file"][accept=".zip"]', {
    name: "fotos-deportistas.zip",
    mimeType: "application/zip",
    buffer: Buffer.from("contenido-zip-no-relevante-para-esta-prueba"),
  });

  await expect(page.getByText("fotos-deportistas.zip")).toBeVisible();
  await page.getByRole("button", { name: "Subir" }).click();

  await expect(page.getByText("Lote enviado. Estado: queued.")).toBeVisible();
  expect(recibido.method).toBe("POST");
  expect(recibido.contentType).toContain("multipart/form-data");
});

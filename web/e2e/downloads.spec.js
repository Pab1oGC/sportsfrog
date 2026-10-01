import { test, expect } from "@playwright/test";
import { loginAs } from "./helpers/auth";

/**
 * Bloque 7, item 5: `lib/download-blob.js` lee `Content-Disposition` para
 * nombrar el archivo descargado, con dos formatos posibles (`filename*=
 * UTF-8''...` primero, `filename="..."` como respaldo) y cae a un nombre fijo
 * cuando el servidor no manda la cabecera -- exactamente lo que jsdom no
 * puede probar: el `<a download>` sintetico y el evento de descarga real del
 * navegador.
 *
 * Se ejercita a traves de "Descargar convocatoria" en /dashboard/competitions
 * (RowActionsMenu -> "..."), un solo call site entre los seis que usan
 * downloadBlob -- todos comparten la misma funcion, asi que probarla una vez
 * a fondo cubre el contrato; lo que cambia entre paginas es solo el boton que
 * la dispara y el nombre de respaldo, ninguno de los dos es lo que este
 * archivo verifica.
 */

async function mockCompetitionsList(page) {
  await page.route("**/api/competitions**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      headers: { "x-total-count": "1" },
      body: JSON.stringify([{ id: 7, name: "Competencia Descargable", sportCode: "futbol", season: "2026", format: "groups", status: "in_progress", isPublic: false, categoryCount: 1 }]),
    });
  });
}

async function abrirMenuYDescargar(page, etiqueta) {
  const fila = page.getByRole("row").filter({ hasText: "Competencia Descargable" });
  await fila.getByRole("button").last().click();
  const [descarga] = await Promise.all([
    page.waitForEvent("download"),
    page.getByRole("menuitem", { name: etiqueta }).click(),
  ]);
  return descarga;
}

test("Content-Disposition con filename*=UTF-8'' nombra el archivo descargado, no el nombre de respaldo", async ({ page }) => {
  await loginAs(page);
  await mockCompetitionsList(page);

  await page.route("**/bulletin.pdf", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/pdf",
      headers: { "content-disposition": "attachment; filename*=UTF-8''convocatoria%20final.pdf" },
      body: "contenido-no-relevante",
    });
  });

  await page.goto("/dashboard/competitions");
  const descarga = await abrirMenuYDescargar(page, "Descargar convocatoria (PDF)");

  expect(descarga.suggestedFilename()).toBe("convocatoria final.pdf");
});

test("sin Content-Disposition, se usa el nombre de respaldo fijo", async ({ page }) => {
  await loginAs(page);
  await mockCompetitionsList(page);

  await page.route("**/bulletin.docx", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      body: "contenido-no-relevante",
    });
  });

  await page.goto("/dashboard/competitions");
  const descarga = await abrirMenuYDescargar(page, "Descargar convocatoria (Word)");

  expect(descarga.suggestedFilename()).toBe("convocatoria.docx");
});

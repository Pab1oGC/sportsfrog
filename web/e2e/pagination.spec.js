import { test, expect } from "@playwright/test";
import { loginAs } from "./helpers/auth";

/**
 * Bloque 7, item 8: la red de seguridad de las tres paginas migradas a
 * paginacion de servidor. La forma es identica en las tres (useCrudDialog +
 * DataGrid con paginationMode="server"), asi que una sola suite tabular las
 * cubre -- mismo espiritu que la suite parametrizada del Bloque 4.
 */
const PAGINAS = [
  { nombre: "athletes", ruta: "/dashboard/athletes", endpoint: "**/api/athletes**", fila: (n) => ({ id: n, firstName: `Nombre${n}`, lastName: `Apellido${n}`, documentId: `DOC${n}`, birthDate: "2000-01-01", gender: "M", isActive: true }) },
  { nombre: "clubs", ruta: "/dashboard/clubs", endpoint: "**/api/clubs**", fila: (n) => ({ id: n, name: `Club ${n}`, shortName: `C${n}`, contactEmail: null, isActive: true }) },
  { nombre: "competitions", ruta: "/dashboard/competitions", endpoint: "**/api/competitions**", fila: (n) => ({ id: n, name: `Competencia ${n}`, season: "2026", status: "in_progress" }) },
];

for (const pagina of PAGINAS) {
  test(`${pagina.nombre} -- cambiar de pagina pide skip/take correctos y el total viene de X-Total-Count`, async ({ page }) => {
    await loginAs(page);

    const pedidos = [];
    await page.route(pagina.endpoint, async (route) => {
      const url = new URL(route.request().url());
      const skip = Number(url.searchParams.get("skip"));
      const take = Number(url.searchParams.get("take"));
      pedidos.push({ skip, take });
      const filas = Array.from({ length: take }, (_, i) => pagina.fila(skip + i + 1));
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        headers: { "x-total-count": "50" },
        body: JSON.stringify(filas),
      });
    });

    await page.goto(pagina.ruta);

    await expect.poll(() => pedidos.length).toBeGreaterThan(0);
    expect(pedidos[0]).toEqual({ skip: 0, take: 25 });
    await expect(page.locator(".MuiTablePagination-displayedRows")).toContainText("50");

    await page.getByRole("button", { name: /pagina siguiente|página siguiente/i }).click();

    await expect.poll(() => pedidos.length).toBeGreaterThan(1);
    expect(pedidos.at(-1)).toEqual({ skip: 25, take: 25 });
  });
}

test("athletes -- el filtro de edad dispara UNA sola peticion al soltar el deslizador, no una por pixel arrastrado", async ({ page }) => {
  await loginAs(page);

  const pedidos = [];
  await page.route("**/api/athletes**", async (route) => {
    const url = new URL(route.request().url());
    pedidos.push(url);
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      headers: { "x-total-count": "0" },
      body: JSON.stringify([]),
    });
  });

  await page.goto("/dashboard/athletes");
  await expect.poll(() => pedidos.length).toBeGreaterThan(0);
  expect(pedidos[0].searchParams.has("minAge")).toBe(false);

  // El thumb inferior del slider de edad: dos sliders en la pagina (edad y
  // peso), dos manijas cada uno -- el primero de los cuatro es el minimo de
  // edad, en el orden en que el JSX los declara.
  const manijaMinima = page.getByRole("slider").first();
  const caja = await manijaMinima.boundingBox();
  if (!caja) throw new Error("No se encontro el deslizador de edad -- selector desactualizado.");

  const y = caja.y + caja.height / 2;
  await page.mouse.move(caja.x + caja.width / 2, y);
  await page.mouse.down();

  // Varios movimientos intermedios simulan un arrastre real -- onChange
  // (visual) puede dispararse en cada uno, pero ninguno debe llegar a la red.
  for (let i = 1; i <= 6; i++) {
    await page.mouse.move(caja.x + caja.width / 2 + i * 6, y);
  }
  expect(pedidos.length).toBe(1);

  await page.mouse.up();

  await expect.poll(() => pedidos.length).toBe(2);
  expect(pedidos.at(-1).searchParams.has("minAge")).toBe(true);
});

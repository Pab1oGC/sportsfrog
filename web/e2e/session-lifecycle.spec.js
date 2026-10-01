import { test, expect } from "@playwright/test";
import { loginAs, fakeJwt } from "./helpers/auth";

/**
 * Bloque 7, item 1 del plan: el ciclo de vida completo de la sesion, con la
 * red interceptada por page.route() -- sin backend real.
 *
 * La garantia de "renovacion de un solo vuelo" ante dos 401 concurrentes ya
 * esta cubierta con precision quirurgica en el Bloque 2 (lib/axios.test.js,
 * con una instancia de axios armada a mano para poder ver exactamente cuantas
 * renovaciones se dispararon). Forzar esa misma condicion de carrera desde un
 * navegador real, a traves de la UI, seria fragil y no agregaria certeza:
 * dependeria de que la pagina elegida dispare dos peticiones verdaderamente
 * simultaneas, algo que ni el plan ni la UI garantizan en ningun punto
 * concreto. Lo que se prueba aca es lo que si necesita un navegador real: el
 * recorrido completo de una sesion -- login, un 401 aislado que se resuelve
 * solo, cierre de sesion, y la persistencia de "recordarme" a traves de una
 * recarga real (la razon de ser del arreglo del alias de vite.config.js).
 */

test("iniciar sesion, un 401 en una peticion autenticada renueva sola y la peticion original se reintenta con exito", async ({ page }) => {
  await loginAs(page, { remember: true });

  let clubsCalls = 0;
  let renewCalls = 0;
  const renewedAccessToken = fakeJwt({ sub: "1", exp: Math.floor(Date.now() / 1000) + 3600 });

  await page.route("**/api/clubs**", async (route) => {
    clubsCalls += 1;
    if (clubsCalls === 1) {
      await route.fulfill({ status: 401, contentType: "application/json", body: JSON.stringify({ detail: "Token expirado" }) });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      headers: { "x-total-count": "1" },
      body: JSON.stringify([{ id: 1, name: "Club de Prueba", shortName: "CDP", contactEmail: null, isActive: true }]),
    });
  });

  await page.route("**/api/auth/session/renewal", async (route) => {
    renewCalls += 1;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ accessToken: renewedAccessToken, refreshToken: "refresh-renovado", organizations: [{ id: 1, slug: "liga-test" }] }),
    });
  });

  await page.goto("/dashboard/clubs");

  // La peticion original (401) se reintenta sola tras la renovacion: sin
  // esperar un segundo llamado a /api/clubs, la fila nunca aparece.
  await expect(page.getByText("Club de Prueba")).toBeVisible();
  expect(clubsCalls).toBe(2);
  expect(renewCalls).toBe(1);

  await expect
    .poll(() => page.evaluate(() => sessionStorage.getItem("jwt_access_token")))
    .toBe(renewedAccessToken);
});

test("cerrar sesion revoca el refresh token en el servidor y limpia el almacenamiento local", async ({ page }) => {
  const { refreshToken } = await loginAs(page, { remember: true });

  let revocationBody = null;
  await page.route("**/api/auth/session/revocation", async (route) => {
    revocationBody = route.request().postDataJSON();
    await route.fulfill({ status: 200, contentType: "application/json", body: "{}" });
  });

  await page.locator(".MuiAvatar-root").click();
  await page.getByRole("menuitem", { name: "Cerrar sesión" }).click();

  await page.waitForURL("**/");
  expect(revocationBody).toEqual({ refreshToken });

  const storageDespues = await page.evaluate(() => ({
    accessToken: sessionStorage.getItem("jwt_access_token"),
    refreshToken: localStorage.getItem("refresh_token"),
    remember: localStorage.getItem("remember_session"),
    organizations: localStorage.getItem("organizations"),
  }));
  expect(storageDespues).toEqual({ accessToken: null, refreshToken: null, remember: null, organizations: null });
});

test.describe("persistencia de 'recordarme' a traves de una recarga", () => {
  for (const remember of [true, false]) {
    test(`recordarme=${remember}: al perder el access token y recargar, la sesion ${remember ? "se renueva sola" : "no se puede renovar"}`, async ({ page }) => {
      await loginAs(page, { remember });

      // Simula que el access token ya no sirve (vencio) sin depender de un
      // reloj real -- es la unica variable que, en la recarga, decide si hace
      // falta el refresh token. Ahi es donde "recordarme" importa: en memoria
      // (sin recordar) no sobrevive a la recarga porque el modulo de
      // session-store se reinstancia entero; en localStorage (recordando) si.
      await page.evaluate(() => sessionStorage.removeItem("jwt_access_token"));

      let renewCalled = false;
      await page.route("**/api/auth/session/renewal", async (route) => {
        renewCalled = true;
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            accessToken: fakeJwt({ sub: "1", exp: Math.floor(Date.now() / 1000) + 3600 }),
            refreshToken: "refresh-renovado",
            organizations: [{ id: 1, slug: "liga-test" }],
          }),
        });
      });

      await page.reload();

      if (remember) {
        // La URL no cambia en este caso (ya estaba en /dashboard y se queda
        // ahi): esperar la URL seria un no-op inmediato mientras
        // checkSession() todavia esta resolviendo. La prueba real de que
        // renovo y quedo autenticado es que el panel llega a renderizarse.
        await expect(page.locator(".MuiAvatar-root")).toBeVisible();
        expect(renewCalled).toBe(true);
      } else {
        await page.waitForURL("**/auth/jwt/sign-in**");
        expect(renewCalled).toBe(false);
      }
    });
  }
});

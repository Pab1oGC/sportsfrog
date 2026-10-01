import { expect } from "@playwright/test";

/**
 * Un JWT sintactico valido (header.payload.firma en base64url) para que
 * `jwtDecode`/`isValidToken` (auth/context/jwt/utils.js) lo acepten sin
 * reventar -- la firma no se valida del lado del cliente, solo el `exp` del
 * payload.
 */
export function fakeJwt(payload) {
  const b64url = (obj) => Buffer.from(JSON.stringify(obj)).toString("base64url");
  return `${b64url({ alg: "none", typ: "JWT" })}.${b64url(payload)}.firma`;
}

export function freshAccessToken() {
  return fakeJwt({ sub: "1", exp: Math.floor(Date.now() / 1000) + 3600 });
}

/**
 * Inicia sesion a traves de la UI real (no sembrando el almacenamiento a
 * mano) para que el ciclo de vida completo -- formulario, POST a la API,
 * guardado del token, redireccion a /dashboard -- corra tal como lo haria
 * una persona real. Devuelve los tokens usados para que la prueba pueda
 * seguir intercept ando peticiones que dependen de ellos (p.ej. la
 * revocacion en el cierre de sesion).
 */
export async function loginAs(page, { remember = false, organizations } = {}) {
  const accessToken = freshAccessToken();
  const refreshToken = "refresh-inicial";
  const orgs = organizations || [{ id: 1, slug: "liga-test", name: "Liga Test" }];

  await page.route("**/api/auth/session", async (route) => {
    if (route.request().method() !== "POST") return route.fallback();
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ accessToken, refreshToken, organizations: orgs }),
    });
  });

  await page.goto("/auth/jwt/sign-in");
  await page.getByLabel("Correo electronico").fill("qa@sportfrog.test");
  await page.getByLabel("Contrasena").fill("Sup3rSecreta!");
  if (remember) {
    // El checkbox esta envuelto en un Tooltip + FormControlLabel con una
    // etiqueta que no es texto plano -- el nombre accesible no siempre se
    // resuelve, asi que se agarra por ser el unico checkbox del formulario.
    await page.locator('input[type="checkbox"]').check();
  }
  await page.getByRole("button", { name: "Iniciar sesion" }).click();
  await page.waitForURL("**/dashboard");
  await expect(page.locator(".MuiAvatar-root")).toBeVisible();

  return { accessToken, refreshToken, organizations: orgs };
}

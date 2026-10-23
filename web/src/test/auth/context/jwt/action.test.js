import { describe, expect, it, vi, beforeEach } from "vitest";
import axios, { endpoints } from "src/lib/axios";
import {
  signIn, signUp, renewSession, signOut,
  requestPasswordReset, resetPassword, changePassword,
} from "src/auth/context/jwt/action";
import { guardarRenovacion, leerRenovacion, seRecuerda, olvidarRenovacion } from "src/auth/context/jwt/session-store";
import { JWT_STORAGE_KEY } from "src/auth/context/jwt/constant";

// action.js es la máquina de estados de la sesión; lib/axios.js es transporte
// (Bloque siguiente de este mismo archivo de prueba). Mockearlo acá aísla la
// lógica de qué se guarda y cuándo, de si el POST realmente viaja por red.
vi.mock("src/lib/axios", () => ({
  default: { post: vi.fn() },
  endpoints: {
    auth: {
      signIn: "/api/auth/session",
      renew: "/api/auth/session/renewal",
      signOut: "/api/auth/session/revocation",
      signUp: "/api/organizations",
      requestPasswordReset: "/api/auth/password/reset-requests",
      resetPassword: "/api/auth/password/reset",
      changePassword: "/api/auth/password",
    },
  },
}));

beforeEach(() => {
  axios.post.mockReset();
  olvidarRenovacion();
});

describe("signIn", () => {
  it("manda email y password al endpoint de sesión", async () => {
    axios.post.mockResolvedValue({ data: { accessToken: "a1", refreshToken: "r1", organizations: [] } });
    await signIn({ email: "coach@club.test", password: "secreta", remember: false });
    expect(axios.post).toHaveBeenCalledWith(endpoints.auth.signIn, { email: "coach@club.test", password: "secreta" });
  });

  it("guarda el access token, el refresh token y las organizaciones al iniciar sesión", async () => {
    axios.post.mockResolvedValue({
      data: { accessToken: "a1", refreshToken: "r1", organizations: [{ id: 1, slug: "liga" }] },
    });
    await signIn({ email: "x@x.test", password: "x", remember: false });

    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBe("a1");
    expect(leerRenovacion()).toBe("r1");
    expect(JSON.parse(localStorage.getItem("organizations"))).toEqual([{ id: 1, slug: "liga" }]);
  });

  it("remember=true persiste el refresh token en localStorage, no solo en memoria", async () => {
    axios.post.mockResolvedValue({ data: { accessToken: "a1", refreshToken: "r1", organizations: [] } });
    await signIn({ email: "x@x.test", password: "x", remember: true });

    expect(localStorage.getItem("refresh_token")).toBe("r1");
    expect(seRecuerda()).toBe(true);
  });

  it("remember ausente se normaliza a false (!!remember)", async () => {
    axios.post.mockResolvedValue({ data: { accessToken: "a1", refreshToken: "r1", organizations: [] } });
    await signIn({ email: "x@x.test", password: "x" });

    expect(seRecuerda()).toBe(false);
    expect(localStorage.getItem("refresh_token")).toBeNull();
  });

  it("devuelve los datos de la sesión (lo que usa el formulario para redirigir)", async () => {
    const datos = { accessToken: "a1", refreshToken: "r1", organizations: [{ id: 1 }] };
    axios.post.mockResolvedValue({ data: datos });
    await expect(signIn({ email: "x@x.test", password: "x" })).resolves.toEqual(datos);
  });

  it("sin localStorage disponible (modo privado), no revienta: la sesión de la pestaña sigue viva", async () => {
    vi.spyOn(window.localStorage, "setItem").mockImplementation(() => {
      throw new Error("quota exceeded");
    });
    axios.post.mockResolvedValue({ data: { accessToken: "a1", refreshToken: "r1", organizations: [{ id: 1 }] } });

    await expect(signIn({ email: "x@x.test", password: "x" })).resolves.toBeDefined();
    // El token en sessionStorage y el refresh token en memoria no dependen
    // de localStorage, así que la sesión sigue siendo usable en esta pestaña.
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBe("a1");
    expect(leerRenovacion()).toBe("r1");
  });
});

describe("signUp", () => {
  it("manda los datos del formulario al endpoint de organizaciones y no toca la sesión", async () => {
    axios.post.mockResolvedValue({ data: { id: 1, slug: "liga-nueva" } });
    const resultado = await signUp({ name: "Liga Nueva" });

    expect(axios.post).toHaveBeenCalledWith(endpoints.auth.signUp, { name: "Liga Nueva" });
    expect(resultado).toEqual({ id: 1, slug: "liga-nueva" });
    // A diferencia de signIn, signUp no inicia sesión por sí solo.
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBeNull();
  });
});

describe("renewSession", () => {
  it("sin refresh token guardado, no llama a la API y devuelve null", async () => {
    const resultado = await renewSession();
    expect(resultado).toBeNull();
    expect(axios.post).not.toHaveBeenCalled();
  });

  it("con refresh token guardado, canjea por una sesión nueva", async () => {
    guardarRenovacion("refresh-viejo", false);
    axios.post.mockResolvedValue({
      data: { accessToken: "a-nuevo", refreshToken: "r-nuevo", organizations: [{ id: 2 }] },
    });

    const resultado = await renewSession();

    expect(axios.post).toHaveBeenCalledWith(endpoints.auth.renew, { refreshToken: "refresh-viejo" });
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBe("a-nuevo");
    expect(leerRenovacion()).toBe("r-nuevo");
    expect(resultado.accessToken).toBe("a-nuevo");
  });

  it("preserva la elección de 'recordarme' de la sesión anterior al renovar", async () => {
    guardarRenovacion("refresh-viejo", true); // sesión anterior con "recordarme" tildado
    axios.post.mockResolvedValue({ data: { accessToken: "a-nuevo", refreshToken: "r-nuevo", organizations: [] } });

    await renewSession();

    // El nuevo refresh token también queda persistido, no solo en memoria,
    // porque seRecuerda() seguía siendo true al momento de renovar.
    expect(localStorage.getItem("refresh_token")).toBe("r-nuevo");
  });

  it("si la API rechaza el refresh token, limpia toda la sesión y devuelve null", async () => {
    guardarRenovacion("refresh-invalido", true);
    sessionStorage.setItem(JWT_STORAGE_KEY, "token-que-ya-no-vale");
    localStorage.setItem("organizations", JSON.stringify([{ id: 1 }]));
    axios.post.mockRejectedValue(new Error("401"));

    const resultado = await renewSession();

    expect(resultado).toBeNull();
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBeNull();
    expect(leerRenovacion()).toBeNull();
    expect(localStorage.getItem("refresh_token")).toBeNull();
    expect(localStorage.getItem("organizations")).toBeNull();
  });
});

describe("signOut", () => {
  it("con un refresh token presente, lo revoca en el servidor antes de limpiar", async () => {
    guardarRenovacion("refresh-activo", true);
    axios.post.mockResolvedValue({});

    await signOut();

    expect(axios.post).toHaveBeenCalledWith(endpoints.auth.signOut, { refreshToken: "refresh-activo" });
    expect(leerRenovacion()).toBeNull();
    expect(localStorage.getItem("refresh_token")).toBeNull();
  });

  it("si la revocación en el servidor falla (sin red, o ya revocado), limpia igual del lado del cliente", async () => {
    guardarRenovacion("refresh-activo", true);
    axios.post.mockRejectedValue(new Error("network down"));

    await expect(signOut()).resolves.toBeUndefined();
    expect(leerRenovacion()).toBeNull();
  });

  it("sin ningún refresh token guardado, no llama a la API pero limpia igual sin reventar", async () => {
    await signOut();
    expect(axios.post).not.toHaveBeenCalled();
  });

  it("borra organizations y expires_in de localStorage al cerrar sesión", async () => {
    guardarRenovacion("refresh-activo", true);
    localStorage.setItem("organizations", JSON.stringify([{ id: 1 }]));
    localStorage.setItem("expires_in", "3600");
    axios.post.mockResolvedValue({});

    await signOut();

    expect(localStorage.getItem("organizations")).toBeNull();
    expect(localStorage.getItem("expires_in")).toBeNull();
  });
});

describe("requestPasswordReset", () => {
  it("manda el correo al endpoint de pedidos y no devuelve nada", async () => {
    axios.post.mockResolvedValue({ status: 202, data: "" });

    const resultado = await requestPasswordReset({ email: "coach@club.test" });

    expect(axios.post).toHaveBeenCalledWith(
      endpoints.auth.requestPasswordReset, { email: "coach@club.test" });
    // La API contesta igual exista o no la cuenta, así que no hay nada que
    // devolverle a la pantalla con lo que pueda distinguir los dos casos.
    expect(resultado).toBeUndefined();
  });

  it("no toca nada de la sesión guardada", async () => {
    guardarRenovacion("refresh-activo", true);
    sessionStorage.setItem(JWT_STORAGE_KEY, "a1");
    axios.post.mockResolvedValue({ status: 202, data: "" });

    await requestPasswordReset({ email: "x@x.test" });

    expect(leerRenovacion()).toBe("refresh-activo");
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBe("a1");
  });

  it("propaga el error del limitador de intentos", async () => {
    axios.post.mockRejectedValue(new Error("Demasiadas solicitudes."));

    await expect(requestPasswordReset({ email: "x@x.test" }))
      .rejects.toThrow("Demasiadas solicitudes.");
  });
});

describe("resetPassword", () => {
  it("manda el token y la contraseña nueva con el nombre que espera la API", async () => {
    axios.post.mockResolvedValue({ status: 204 });

    await resetPassword({ token: "t-1", password: "Nueva-Clave-2026!" });

    expect(axios.post).toHaveBeenCalledWith(
      endpoints.auth.resetPassword, { token: "t-1", newPassword: "Nueva-Clave-2026!" });
  });

  it("limpia la sesión local al terminar: la API ya revocó todas", async () => {
    guardarRenovacion("refresh-viejo", true);
    sessionStorage.setItem(JWT_STORAGE_KEY, "token-que-ya-no-vale");
    localStorage.setItem("organizations", JSON.stringify([{ id: 1 }]));
    localStorage.setItem("expires_in", "3600");
    axios.post.mockResolvedValue({ status: 204 });

    await resetPassword({ token: "t-1", password: "Nueva-Clave-2026!" });

    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBeNull();
    expect(leerRenovacion()).toBeNull();
    expect(localStorage.getItem("refresh_token")).toBeNull();
    expect(localStorage.getItem("organizations")).toBeNull();
    expect(localStorage.getItem("expires_in")).toBeNull();
  });

  it("no revoca por su cuenta: el servidor ya lo hizo, un POST más no agrega nada", async () => {
    guardarRenovacion("refresh-viejo", true);
    axios.post.mockResolvedValue({ status: 204 });

    await resetPassword({ token: "t-1", password: "Nueva-Clave-2026!" });

    expect(axios.post).toHaveBeenCalledTimes(1);
    expect(axios.post).not.toHaveBeenCalledWith(endpoints.auth.signOut, expect.anything());
  });

  it("si la API rechaza el enlace, propaga el error y deja la sesión como estaba", async () => {
    guardarRenovacion("refresh-activo", true);
    axios.post.mockRejectedValue(new Error("El enlace de recuperación no es válido o ya venció."));

    await expect(resetPassword({ token: "vencido", password: "Nueva-Clave-2026!" }))
      .rejects.toThrow("no es válido o ya venció");

    // No se cambió ninguna contraseña, así que nada hay que limpiar: quien
    // tenía sesión abierta la conserva.
    expect(leerRenovacion()).toBe("refresh-activo");
  });
});

describe("changePassword", () => {
  it("manda la actual y la nueva, y limpia la sesión local (la API revoca todas)", async () => {
    guardarRenovacion("refresh-activo", true);
    sessionStorage.setItem(JWT_STORAGE_KEY, "a1");
    axios.post.mockResolvedValue({ status: 204 });

    await changePassword({ currentPassword: "Vieja-2026!", newPassword: "Nueva-Clave-2026!" });

    expect(axios.post).toHaveBeenCalledWith(
      endpoints.auth.changePassword,
      { currentPassword: "Vieja-2026!", newPassword: "Nueva-Clave-2026!" });
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBeNull();
    expect(leerRenovacion()).toBeNull();
  });

  it("si la contraseña actual es incorrecta, propaga el error y conserva la sesión", async () => {
    guardarRenovacion("refresh-activo", true);
    axios.post.mockRejectedValue(new Error("La contraseña actual es incorrecta."));

    await expect(changePassword({ currentPassword: "mal", newPassword: "Nueva-Clave-2026!" }))
      .rejects.toThrow("La contraseña actual es incorrecta.");

    expect(leerRenovacion()).toBe("refresh-activo");
  });
});

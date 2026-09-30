import { describe, expect, it, vi, beforeEach } from "vitest";

/**
 * axios.create() se reemplaza por una instancia falsa que:
 *  - es invocable (axiosInstance(config)) -- lib/axios.js llama así al
 *    reintento tras renovar ("return axiosInstance(config)").
 *  - expone .get/.post/.put/.delete como vi.fn() controlables.
 *  - captura los interceptores registrados en vez de ejecutarlos de verdad,
 *    para poder invocarlos directamente con un config/error armado a mano y
 *    ver qué hacen -- sin tocar la red ni depender de axios real.
 *
 * Definida dentro de vi.hoisted porque vi.mock("axios", ...) se iza por
 * encima de los imports normales, y solo puede referenciar variables que
 * también estén izadas.
 */
const { crearInstanciaFalsa, instancias } = vi.hoisted(() => {
  const instancias = [];
  function crearInstanciaFalsa() {
    const instancia = vi.fn();
    instancia.get = vi.fn();
    instancia.post = vi.fn();
    instancia.put = vi.fn();
    instancia.delete = vi.fn();
    instancia.interceptors = {
      request: { handlers: [], use(fn) { this.handlers.push(fn); } },
      response: { handlers: [], use(onFulfilled, onRejected) { this.handlers.push({ onFulfilled, onRejected }); } },
    };
    instancias.push(instancia);
    return instancia;
  }
  return { crearInstanciaFalsa, instancias };
});

vi.mock("axios", () => ({
  default: { create: vi.fn(crearInstanciaFalsa) },
}));

// El módulo bajo prueba llama axios.create() una sola vez, al importarse --
// esa única instancia falsa es la que queda en instancias[0].
import { endpoints } from "src/lib/axios";
import { JWT_STORAGE_KEY } from "src/auth/context/jwt/constant";
import { guardarRenovacion, leerRenovacion, olvidarRenovacion } from "src/auth/context/jwt/session-store";

const instancia = instancias[0];
const interceptorDePedido = instancia.interceptors.request.handlers[0];
const interceptorDeRespuesta = instancia.interceptors.response.handlers[0];

function configDePedido(overrides = {}) {
  return { headers: {}, ...overrides };
}

function errorDeRespuesta({ status, data, url, retried = false, sinRespuesta = false, message } = {}) {
  const config = { url, _retriedAfterRenewal: retried, headers: {} };
  const error = { config, message };
  if (!sinRespuesta) error.response = { status, data };
  return error;
}

/**
 * `window.location.href` no se puede espiar directamente con vi.spyOn: la
 * propiedad `href` del propio objeto Location no es configurable en jsdom
 * ("Cannot redefine property: href"). `window.location` en sí sí lo es, así
 * que se reemplaza el objeto entero por uno controlable.
 */
function reemplazarLocation() {
  const descriptorOriginal = Object.getOwnPropertyDescriptor(window, "location");
  Object.defineProperty(window, "location", { configurable: true, value: { href: "" } });
  return {
    get href() {
      return window.location.href;
    },
    restaurar() {
      Object.defineProperty(window, "location", descriptorOriginal);
    },
  };
}

beforeEach(() => {
  olvidarRenovacion();
  instancia.post.mockReset();
  instancia.get.mockReset();
  instancia.mockReset();
});

describe("interceptor de pedido", () => {
  it("agrega Authorization desde sessionStorage cuando hay token", () => {
    sessionStorage.setItem(JWT_STORAGE_KEY, "mi-token");
    const config = interceptorDePedido(configDePedido());
    expect(config.headers.Authorization).toBe("Bearer mi-token");
  });

  it("no agrega Authorization si no hay token en sessionStorage", () => {
    const config = interceptorDePedido(configDePedido());
    expect(config.headers.Authorization).toBeUndefined();
  });

  it("agrega X-Organization-Id desde la primera organización guardada", () => {
    localStorage.setItem("organizations", JSON.stringify([{ id: 42 }, { id: 99 }]));
    const config = interceptorDePedido(configDePedido());
    expect(config.headers["X-Organization-Id"]).toBe(42);
  });

  it("sin organizaciones guardadas, no agrega X-Organization-Id", () => {
    const config = interceptorDePedido(configDePedido());
    expect(config.headers["X-Organization-Id"]).toBeUndefined();
  });

  it("JSON corrupto en 'organizations' no revienta el pedido: sigue sin la cabecera", () => {
    localStorage.setItem("organizations", "{esto no es json");
    expect(() => interceptorDePedido(configDePedido())).not.toThrow();
    expect(interceptorDePedido(configDePedido()).headers["X-Organization-Id"]).toBeUndefined();
  });

  it("body FormData: borra Content-Type para que el navegador ponga el boundary multiparte", () => {
    const config = interceptorDePedido(configDePedido({ headers: { "Content-Type": "application/json" }, data: new FormData() }));
    expect(config.headers["Content-Type"]).toBeUndefined();
  });

  it("body normal (no FormData): no toca Content-Type", () => {
    const config = interceptorDePedido(
      configDePedido({ headers: { "Content-Type": "application/json" }, data: { a: 1 } }),
    );
    expect(config.headers["Content-Type"]).toBe("application/json");
  });

  it("devuelve el mismo objeto config (para que axios siga la cadena de interceptores)", () => {
    const config = configDePedido();
    expect(interceptorDePedido(config)).toBe(config);
  });
});

describe("interceptor de respuesta -- camino feliz", () => {
  it("una respuesta exitosa pasa intacta", () => {
    const respuesta = { status: 200, data: { ok: true } };
    expect(interceptorDeRespuesta.onFulfilled(respuesta)).toBe(respuesta);
  });
});

describe("interceptor de respuesta -- renovación automática en un 401", () => {
  it("con refresh token disponible, renueva y reintenta el pedido original", async () => {
    guardarRenovacion("refresh-1", false);
    instancia.post.mockResolvedValue({ data: { accessToken: "a-nuevo", refreshToken: "r-nuevo", organizations: [] } });
    instancia.mockResolvedValue({ data: "respuesta-del-reintento" });

    const error = errorDeRespuesta({ status: 401, data: { detail: "vencido" }, url: "/api/athletes" });
    const resultado = await interceptorDeRespuesta.onRejected(error);

    expect(instancia.post).toHaveBeenCalledWith(endpoints.auth.renew, { refreshToken: "refresh-1" });
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBe("a-nuevo");
    expect(leerRenovacion()).toBe("r-nuevo");
    // El pedido original se reintenta con el mismo config, ya marcado.
    expect(error.config._retriedAfterRenewal).toBe(true);
    expect(instancia).toHaveBeenCalledWith(error.config);
    expect(resultado).toEqual({ data: "respuesta-del-reintento" });
  });

  it("sin refresh token disponible, no reintenta: manda a la pantalla de login", async () => {
    const location = reemplazarLocation();
    try {
      const error = errorDeRespuesta({ status: 401, data: {}, url: "/api/athletes" });
      await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow();

      expect(instancia.post).not.toHaveBeenCalled(); // renovarSesion no llega a pedir nada sin refresh token
      expect(location.href).toBe("/auth/jwt/sign-in");
    } finally {
      location.restaurar();
    }
  });

  it("si el servidor rechaza el refresh token, también manda a la pantalla de login", async () => {
    guardarRenovacion("refresh-invalido", false);
    instancia.post.mockRejectedValue(new Error("401 en la renovación"));
    const location = reemplazarLocation();
    try {
      const error = errorDeRespuesta({ status: 401, data: {}, url: "/api/athletes" });
      await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow();

      expect(location.href).toBe("/auth/jwt/sign-in");
      expect(leerRenovacion()).toBeNull(); // la sesión quedó limpia
    } finally {
      location.restaurar();
    }
  });

  it("un 401 en el propio endpoint de renovación NO dispara una renovación (evita el bucle infinito)", async () => {
    guardarRenovacion("refresh-1", false);
    const error = errorDeRespuesta({ status: 401, data: { detail: "refresh inválido" }, url: endpoints.auth.renew });

    await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("refresh inválido");
    expect(instancia.post).not.toHaveBeenCalled();
  });

  it("un 401 en el propio login (contraseña incorrecta) NO dispara una renovación", async () => {
    guardarRenovacion("refresh-1", false);
    const error = errorDeRespuesta({ status: 401, data: { detail: "Credenciales inválidas" }, url: endpoints.auth.signIn });

    await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("Credenciales inválidas");
    expect(instancia.post).not.toHaveBeenCalled();
  });

  it("un pedido ya reintentado que vuelve a dar 401 no reintenta de nuevo (corta el bucle)", async () => {
    guardarRenovacion("refresh-1", false);
    const error = errorDeRespuesta({ status: 401, data: { detail: "sigue sin autorizar" }, url: "/api/athletes", retried: true });

    await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("sigue sin autorizar");
    expect(instancia.post).not.toHaveBeenCalled();
  });

  it("dos 401 concurrentes comparten UNA sola renovación (el refresh token rota del lado servidor)", async () => {
    guardarRenovacion("refresh-1", false);
    let resolverRenovacion;
    instancia.post.mockReturnValue(
      new Promise((resolve) => {
        resolverRenovacion = resolve;
      }),
    );
    instancia.mockResolvedValue({ data: "reintentado" });

    const errorA = errorDeRespuesta({ status: 401, data: {}, url: "/api/athletes" });
    const errorB = errorDeRespuesta({ status: 401, data: {}, url: "/api/clubs" });

    // Los dos pedidos vencen "al mismo tiempo": ninguno espera al otro antes de arrancar.
    const promesaA = interceptorDeRespuesta.onRejected(errorA);
    const promesaB = interceptorDeRespuesta.onRejected(errorB);

    resolverRenovacion({ data: { accessToken: "a-nuevo", refreshToken: "r-nuevo", organizations: [] } });
    await Promise.all([promesaA, promesaB]);

    expect(instancia.post).toHaveBeenCalledTimes(1);
  });

  it("una renovación completa no reutiliza la promesa ya resuelta: una renovación posterior vuelve a pedir", async () => {
    guardarRenovacion("refresh-1", false);
    instancia.post.mockResolvedValueOnce({ data: { accessToken: "a1", refreshToken: "r1", organizations: [] } });
    instancia.mockResolvedValue({ data: "ok" });

    await interceptorDeRespuesta.onRejected(errorDeRespuesta({ status: 401, data: {}, url: "/api/athletes" }));
    expect(instancia.post).toHaveBeenCalledTimes(1);

    instancia.post.mockResolvedValueOnce({ data: { accessToken: "a2", refreshToken: "r2", organizations: [] } });
    await interceptorDeRespuesta.onRejected(errorDeRespuesta({ status: 401, data: {}, url: "/api/athletes" }));
    expect(instancia.post).toHaveBeenCalledTimes(2);
  });
});

describe("interceptor de respuesta -- normalización del mensaje de error", () => {
  it("prioriza 'detail' sobre cualquier otro campo", () => {
    const error = errorDeRespuesta({
      status: 400,
      data: { detail: "El nombre es obligatorio", title: "Bad Request", errors: { name: ["requerido"] } },
    });
    return expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("El nombre es obligatorio");
  });

  it("sin 'detail', usa los mensajes de 'errors' (fieldErrors), uniendo todos los campos", () => {
    const error = errorDeRespuesta({
      status: 400,
      data: { title: "Bad Request", errors: { name: ["Es obligatorio"], email: ["No es válido"] } },
    });
    return expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("Es obligatorio No es válido");
  });

  it("un campo con un solo string (no arreglo) en 'errors' también se incluye", () => {
    const error = errorDeRespuesta({ status: 400, data: { errors: { name: "Es obligatorio" } } });
    return expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("Es obligatorio");
  });

  it("sin 'detail' ni 'errors', usa 'title'", () => {
    const error = errorDeRespuesta({ status: 400, data: { title: "Bad Request" } });
    return expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("Bad Request");
  });

  it("sin body útil, usa error.message (por ejemplo, un error de red sin respuesta)", async () => {
    const error = errorDeRespuesta({ sinRespuesta: true, message: "Network Error" });
    await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("Network Error");
  });

  it("sin absolutamente nada, cae en el literal 'Error'", async () => {
    const error = { config: { headers: {} } };
    await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("Error");
  });

  it("conserva status, response y errors en el Error que rechaza (para que la pantalla los lea)", async () => {
    const error = errorDeRespuesta({ status: 409, data: { detail: "Conflicto", errors: { a: ["x"] } } });
    try {
      await interceptorDeRespuesta.onRejected(error);
      throw new Error("no debería resolver");
    } catch (fallo) {
      expect(fallo.status).toBe(409);
      expect(fallo.response).toEqual(error.response);
      expect(fallo.errors).toEqual({ a: ["x"] });
    }
  });

  it("un cuerpo de error que llega como Blob (descarga con responseType 'blob') se desempaqueta antes de leer 'detail'", async () => {
    const blob = new Blob([JSON.stringify({ detail: "No se pudo generar el PDF" })], { type: "application/json" });
    const error = errorDeRespuesta({ status: 500, data: blob });
    await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("No se pudo generar el PDF");
  });

  it("un Blob que no contiene JSON válido no revienta: cae en el mensaje genérico", async () => {
    const blob = new Blob(["esto no es json"], { type: "text/plain" });
    const error = errorDeRespuesta({ status: 500, data: blob, message: "Request failed" });
    await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("Request failed");
  });
});

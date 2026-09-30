import { describe, expect, it, vi, beforeEach } from "vitest";

// Mismo enfoque que axios.test.js: axios.create() se reemplaza por una
// instancia falsa cuyos interceptores se capturan para invocarlos a mano.
// public-axios.js es deliberadamente más simple que lib/axios.js -- sin
// interceptor de pedido, sin renovación, sin cabecera de organización -- y
// esta suite existe sobre todo para fijar esa asimetría, no para repetir
// las pruebas de axios.test.js.
const { crearInstanciaFalsa, instancias } = vi.hoisted(() => {
  const instancias = [];
  function crearInstanciaFalsa() {
    const instancia = vi.fn();
    instancia.get = vi.fn();
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

import { publicFetcher } from "src/lib/public-axios";

const instancia = instancias[0];
const interceptorDeRespuesta = instancia.interceptors.response.handlers[0];

beforeEach(() => {
  instancia.get.mockReset();
});

describe("public-axios -- interceptor de respuesta", () => {
  it("no tiene interceptor de pedido: a diferencia de lib/axios.js, no agrega Authorization ni X-Organization-Id", () => {
    expect(instancia.interceptors.request.handlers).toHaveLength(0);
  });

  it("una respuesta exitosa pasa intacta", () => {
    const respuesta = { status: 200, data: [] };
    expect(interceptorDeRespuesta.onFulfilled(respuesta)).toBe(respuesta);
  });

  it("usa response.data.detail cuando está presente", async () => {
    const error = { response: { data: { detail: "Competencia no encontrada o no publicada." } } };
    await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("Competencia no encontrada o no publicada.");
  });

  it("sin 'detail', cae en error.message -- no tiene el fieldErrors/title de lib/axios.js", async () => {
    const error = { response: { data: { title: "Bad Request", errors: { a: ["x"] } } }, message: "Request failed with status code 400" };
    await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("Request failed with status code 400");
  });

  it("sin detail ni message, cae en el literal 'Error'", async () => {
    const error = {};
    await expect(interceptorDeRespuesta.onRejected(error)).rejects.toThrow("Error");
  });

  it("el error rechazado es un Error simple, sin .status/.response/.errors (a diferencia de lib/axios.js)", async () => {
    const error = { response: { status: 404, data: { detail: "No encontrado" } } };
    try {
      await interceptorDeRespuesta.onRejected(error);
      throw new Error("no debería resolver");
    } catch (fallo) {
      expect(fallo.message).toBe("No encontrado");
      expect(fallo.status).toBeUndefined();
    }
  });
});

describe("publicFetcher", () => {
  it("pide la url y devuelve solo el cuerpo de la respuesta (para usar directo con SWR)", async () => {
    instancia.get.mockResolvedValue({ data: { standings: [] } });
    await expect(publicFetcher("/api/public/liga/torneo/standings")).resolves.toEqual({ standings: [] });
    expect(instancia.get).toHaveBeenCalledWith("/api/public/liga/torneo/standings");
  });
});

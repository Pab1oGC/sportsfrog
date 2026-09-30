import { describe, expect, it, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { SWRConfig } from "swr";

vi.mock("src/lib/axios", () => ({
  default: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}));

import axios from "src/lib/axios";
import { useApi, apiPost, apiPut, apiDelete } from "src/hooks/use-api";

// Caché de SWR nueva por prueba: sin esto, dos pruebas que pidan la misma
// url verían la respuesta que dejó la anterior en vez de volver a pedirla.
function wrapper({ children }) {
  return <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>{children}</SWRConfig>;
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("useApi", () => {
  it("data es el cuerpo de la respuesta, no el sobre {data, totalCount} interno del fetcher", async () => {
    const filas = [{ id: "1" }, { id: "2" }];
    axios.get.mockResolvedValue({ data: filas, headers: {} });

    const { result } = renderHook(() => useApi("/api/clubs"), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.data).toBe(filas);
  });

  it("totalCount es null cuando la respuesta no trae X-Total-Count (listado sin paginar)", async () => {
    axios.get.mockResolvedValue({ data: [], headers: {} });
    const { result } = renderHook(() => useApi("/api/clubs"), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.totalCount).toBeNull();
  });

  it("totalCount es un número cuando la cabecera está presente (en minúsculas, como la normaliza axios)", async () => {
    axios.get.mockResolvedValue({ data: [], headers: { "x-total-count": "42" } });
    const { result } = renderHook(() => useApi("/api/athletes?skip=0&take=10"), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.totalCount).toBe(42);
  });

  it("una página vacía real (X-Total-Count: '0') da 0, no null -- != null es el guard correcto, no un chequeo de verdad", async () => {
    axios.get.mockResolvedValue({ data: [], headers: { "x-total-count": "0" } });
    const { result } = renderHook(() => useApi("/api/athletes?skip=0&take=10"), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.totalCount).toBe(0);
  });

  it("arranca en isLoading=true y pasa a false cuando la respuesta llega", async () => {
    let resolverPedido;
    axios.get.mockReturnValue(
      new Promise((resolve) => {
        resolverPedido = resolve;
      }),
    );

    const { result } = renderHook(() => useApi("/api/clubs"), { wrapper });
    expect(result.current.isLoading).toBe(true);

    resolverPedido({ data: [], headers: {} });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it("expone el error cuando el pedido falla", async () => {
    axios.get.mockRejectedValue(new Error("Error de red"));
    const { result } = renderHook(() => useApi("/api/clubs"), { wrapper });
    await waitFor(() => expect(result.current.error).toBeDefined());
    expect(result.current.error.message).toBe("Error de red");
  });

  it("url === null desactiva el pedido por completo -- no llama a axios.get", async () => {
    const { result } = renderHook(() => useApi(null), { wrapper });
    // Se le da una vuelta de reloj para que, si algo llamara a axios.get, ya
    // hubiera pasado -- SWR resuelve las claves en un microtask.
    await Promise.resolve();
    expect(axios.get).not.toHaveBeenCalled();
    expect(result.current.isLoading).toBe(false);
    expect(result.current.data).toBeUndefined();
  });

  it("mutate es una función utilizable para forzar un refetch", async () => {
    axios.get.mockResolvedValue({ data: [{ id: "1" }], headers: {} });
    const { result } = renderHook(() => useApi("/api/clubs"), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(typeof result.current.mutate).toBe("function");

    axios.get.mockClear();
    axios.get.mockResolvedValue({ data: [{ id: "1" }, { id: "2" }], headers: {} });
    await result.current.mutate();

    await waitFor(() => expect(result.current.data).toHaveLength(2));
    expect(axios.get).toHaveBeenCalledOnce();
  });

  it("revalidateOnFocus:false es el default, pero options puede pisarlo -- se pasan después en el spread", async () => {
    // No se simula el evento de foco de la ventana acá (frágil e innecesario);
    // alcanza con demostrar que una opción propia de SWR pasada en `options`
    // (un callback, en este caso) efectivamente llega hasta useSWR.
    const onSuccess = vi.fn();
    axios.get.mockResolvedValue({ data: [{ id: "1" }], headers: {} });
    renderHook(() => useApi("/api/clubs", { onSuccess }), { wrapper });
    await waitFor(() => expect(onSuccess).toHaveBeenCalled());
  });
});

describe("apiPost / apiPut / apiDelete", () => {
  it("apiPost manda el cuerpo y devuelve response.data", async () => {
    axios.post.mockResolvedValue({ data: { id: "1" } });
    await expect(apiPost("/api/clubs", { nombre: "ACME" })).resolves.toEqual({ id: "1" });
    expect(axios.post).toHaveBeenCalledWith("/api/clubs", { nombre: "ACME" });
  });

  it("apiPut manda el cuerpo y devuelve response.data", async () => {
    axios.put.mockResolvedValue({ data: { id: "1", nombre: "ACME" } });
    await expect(apiPut("/api/clubs/1", { nombre: "ACME" })).resolves.toEqual({ id: "1", nombre: "ACME" });
  });

  it("apiDelete no manda cuerpo y devuelve response.data", async () => {
    axios.delete.mockResolvedValue({ data: {} });
    await expect(apiDelete("/api/clubs/1")).resolves.toEqual({});
    expect(axios.delete).toHaveBeenCalledWith("/api/clubs/1");
  });

  it("un rechazo de axios se propaga tal cual (la pantalla decide cómo mostrarlo)", async () => {
    axios.post.mockRejectedValue(new Error("El nombre ya está en uso."));
    await expect(apiPost("/api/clubs", {})).rejects.toThrow("El nombre ya está en uso.");
  });
});

import { describe, expect, it, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { endpoints } from "src/lib/axios";

// Se mockea solo useApi -- el nivel de obtención de datos -- y se dejan
// reales useLastCompetition/useRememberedChild (ya probados aparte), para
// que esta suite ejercite lo que es realmente propio de useCascade: la
// composición de los tres niveles y el encadenado de `null` cuando el padre
// todavía no se eligió.
vi.mock("src/hooks/use-api", () => ({ useApi: vi.fn() }));
import { useApi } from "src/hooks/use-api";
import { useCascade } from "src/hooks/use-cascade";

beforeEach(() => {
  localStorage.clear();
});

/** Arma un useApi falso que responde según la url pedida, de forma estable entre renders. */
function useApiFalso(porUrl) {
  return (url) => {
    if (url == null) return { data: undefined, isLoading: false, totalCount: null, error: undefined, mutate: vi.fn() };
    const entrada = porUrl[url];
    return entrada
      ? { data: entrada, isLoading: false, totalCount: null, error: undefined, mutate: vi.fn() }
      : { data: undefined, isLoading: true, totalCount: null, error: undefined, mutate: vi.fn() };
  };
}

function llamoConUrlQueContiene(fragmento) {
  return useApi.mock.calls.some(([url]) => typeof url === "string" && url.includes(fragmento));
}

describe("useCascade", () => {
  it("sin ninguna competencia disponible, categorías y equipos se piden con url null (nunca se llega a pedir nada)", async () => {
    useApi.mockImplementation(useApiFalso({ [endpoints.competitions]: [] }));
    const { result } = renderHook(() => useCascade());

    await waitFor(() => expect(result.current.loadingComps).toBe(false));

    expect(llamoConUrlQueContiene("/categories")).toBe(false);
    expect(llamoConUrlQueContiene("/teams")).toBe(false);
    expect(result.current.catId).toBe("");
    expect(result.current.teamId).toBe("");
  });

  it("con una competencia resuelta pero sin categorías todavía, pide categorías con la url real y equipos con null", async () => {
    useApi.mockImplementation(
      useApiFalso({
        [endpoints.competitions]: [{ id: "c1", status: "in_progress" }],
        [endpoints.categories("c1")]: [],
      }),
    );
    const { result } = renderHook(() => useCascade());

    await waitFor(() => expect(result.current.compId).toBe("c1"));
    await waitFor(() => expect(result.current.loadingCats).toBe(false));

    expect(useApi).toHaveBeenCalledWith(endpoints.categories("c1"));
    expect(llamoConUrlQueContiene("/teams")).toBe(false);
    expect(result.current.catId).toBe("");
  });

  it("con los tres niveles resueltos, pide cada uno con la url real que depende del padre elegido", async () => {
    useApi.mockImplementation(
      useApiFalso({
        [endpoints.competitions]: [{ id: "c1", status: "in_progress" }],
        [endpoints.categories("c1")]: [{ id: "cat1" }],
        [endpoints.teams("cat1")]: [{ id: "team1" }],
      }),
    );
    const { result } = renderHook(() => useCascade());

    await waitFor(() => expect(result.current.teamId).toBe("team1"));

    expect(result.current.compId).toBe("c1");
    expect(result.current.catId).toBe("cat1");
    expect(useApi).toHaveBeenCalledWith(endpoints.categories("c1"));
    expect(useApi).toHaveBeenCalledWith(endpoints.teams("cat1"));
  });

  it("expone las tres listas como arreglo vacío mientras no llegó nada, nunca undefined", () => {
    useApi.mockImplementation(useApiFalso({}));
    const { result } = renderHook(() => useCascade());

    expect(result.current.competiciones).toEqual([]);
    expect(result.current.categorias).toEqual([]);
    expect(result.current.equipos).toEqual([]);
  });

  it("propaga loadingComps tal cual desde useApi; loadingCats/loadingTeams son false mientras están deshabilitados (url null), no true", () => {
    useApi.mockImplementation(useApiFalso({})); // competitions todavía sin responder
    const { result } = renderHook(() => useCascade());

    // Sin competencia resuelta, categorías y equipos piden con url null --
    // un pedido deshabilitado de useApi/SWR no está "cargando", simplemente
    // no hay nada en vuelo.
    expect(result.current.loadingComps).toBe(true);
    expect(result.current.loadingCats).toBe(false);
    expect(result.current.loadingTeams).toBe(false);
  });

  it("un initialCompId explícito de la URL se respeta si existe en la lista de competencias", async () => {
    useApi.mockImplementation(
      useApiFalso({
        [endpoints.competitions]: [
          { id: "c1", status: "in_progress" },
          { id: "c2", status: "finished" },
        ],
        [endpoints.categories("c2")]: [],
      }),
    );
    const { result } = renderHook(() => useCascade("c2"));

    await waitFor(() => expect(result.current.compId).toBe("c2"));
    // No la que está "en curso" (c1) -- la de la URL gana, como documenta
    // useLastCompetition.
    expect(useApi).toHaveBeenCalledWith(endpoints.categories("c2"));
  });

  it("setCompId/setCatId/setTeamId son los setters expuestos por cada nivel encadenado", () => {
    useApi.mockImplementation(useApiFalso({}));
    const { result } = renderHook(() => useCascade());

    expect(typeof result.current.setCompId).toBe("function");
    expect(typeof result.current.setCatId).toBe("function");
    expect(typeof result.current.setTeamId).toBe("function");
  });
});

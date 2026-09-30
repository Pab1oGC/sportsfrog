import { describe, expect, it, vi, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useLastCompetition } from "src/hooks/use-last-competition";

const STORAGE_KEY = "sportfrog:lastCompetitionId";

const competiciones = [
  { id: "c1", status: "finished" },
  { id: "c2", status: "in_progress" },
  { id: "c3", status: "scheduled" },
];

beforeEach(() => {
  localStorage.clear();
});

describe("resolución inicial (orden de precedencia)", () => {
  it("un id de la URL que existe en la lista gana sobre todo lo demás", () => {
    localStorage.setItem(STORAGE_KEY, "c3"); // aunque haya algo recordado...
    const { result } = renderHook(() => useLastCompetition(competiciones, "c1")); // ...la URL manda
    expect(result.current[0]).toBe("c1");
  });

  it("un id de la URL que NO existe en la lista se ignora y sigue la cadena de resolución", () => {
    const { result } = renderHook(() => useLastCompetition(competiciones, "id-inexistente"));
    // Cae en "en curso", el siguiente escalón.
    expect(result.current[0]).toBe("c2");
  });

  it("sin id de URL, usa lo recordado en localStorage si sigue existiendo en la lista", () => {
    localStorage.setItem(STORAGE_KEY, "c1");
    const { result } = renderHook(() => useLastCompetition(competiciones, ""));
    expect(result.current[0]).toBe("c1");
  });

  it("lo recordado en localStorage que ya no existe en la lista se ignora", () => {
    localStorage.setItem(STORAGE_KEY, "id-que-ya-no-existe");
    const { result } = renderHook(() => useLastCompetition(competiciones, ""));
    expect(result.current[0]).toBe("c2"); // cae en "en curso"
  });

  it("sin URL ni nada recordado, elige la que está en curso, aunque no sea la primera", () => {
    const { result } = renderHook(() => useLastCompetition(competiciones, ""));
    expect(result.current[0]).toBe("c2");
  });

  it("sin ninguna en curso, cae en la primera de la lista", () => {
    const sinEnCurso = [{ id: "c1", status: "finished" }, { id: "c2", status: "scheduled" }];
    const { result } = renderHook(() => useLastCompetition(sinEnCurso, ""));
    expect(result.current[0]).toBe("c1");
  });

  it("mientras competiciones es undefined o [], no resuelve nada (queda en el valor inicial, sin reventar)", () => {
    const { result, rerender } = renderHook(({ comps, url }) => useLastCompetition(comps, url), {
      initialProps: { comps: undefined, url: "" },
    });
    expect(result.current[0]).toBe("");

    rerender({ comps: [], url: "" });
    expect(result.current[0]).toBe("");
  });

  it("con un id de URL pero sin lista todavía, arranca mostrando ese id (optimista) sin esperar la resolución", () => {
    const { result } = renderHook(() => useLastCompetition(undefined, "c1"));
    expect(result.current[0]).toBe("c1");
  });
});

describe("resuelve una sola vez", () => {
  it("una vez resuelto, un cambio posterior en la lista no vuelve a recalcular", () => {
    const { result, rerender } = renderHook(({ comps }) => useLastCompetition(comps, ""), {
      initialProps: { comps: competiciones },
    });
    expect(result.current[0]).toBe("c2"); // la que estaba en curso

    // Ahora la lista cambia: otra competencia pasó a estar en curso.
    const listaActualizada = [
      { id: "c1", status: "in_progress" },
      { id: "c2", status: "finished" },
    ];
    rerender({ comps: listaActualizada });

    // No se mueve: la resolución ya ocurrió una vez.
    expect(result.current[0]).toBe("c2");
  });

  it("un urlCompId que llega tarde (tras el primer render con la lista ya cargada) se ignora", () => {
    const { result, rerender } = renderHook(({ url }) => useLastCompetition(competiciones, url), {
      initialProps: { url: "" },
    });
    expect(result.current[0]).toBe("c2");

    rerender({ url: "c1" }); // llega después de que ya se resolvió
    expect(result.current[0]).toBe("c2"); // sigue sin moverse
  });
});

describe("setCompId", () => {
  it("actualiza el estado y persiste el id elegido en localStorage", () => {
    const { result } = renderHook(() => useLastCompetition([], ""));
    act(() => result.current[1]("c9"));
    expect(result.current[0]).toBe("c9");
    expect(localStorage.getItem(STORAGE_KEY)).toBe("c9");
  });

  it("elegir un id vacío borra lo guardado en localStorage", () => {
    localStorage.setItem(STORAGE_KEY, "c1");
    const { result } = renderHook(() => useLastCompetition([], ""));
    act(() => result.current[1](""));
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it("si localStorage no está disponible, no revienta (ni al leer ni al escribir)", () => {
    const setItemEspiado = vi.spyOn(window.localStorage, "setItem").mockImplementation(() => {
      throw new Error("bloqueado");
    });
    const { result } = renderHook(() => useLastCompetition([], ""));
    expect(() => act(() => result.current[1]("c9"))).not.toThrow();
    expect(result.current[0]).toBe("c9"); // el estado en memoria igual se actualiza
    setItemEspiado.mockRestore();
  });
});

import { describe, expect, it, vi, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useRememberedChild } from "src/hooks/use-remembered-child";

const PREFIX = "sportfrog:lastCategoryId:";
const categorias = [{ id: "cat1" }, { id: "cat2" }];

beforeEach(() => {
  localStorage.clear();
});

describe("resolución inicial (por padre)", () => {
  it("usa lo recordado para ese padre si el hijo sigue existiendo en la lista", () => {
    localStorage.setItem(PREFIX + "comp1", "cat2");
    const { result } = renderHook(() => useRememberedChild(categorias, "comp1", PREFIX));
    expect(result.current[0]).toBe("cat2");
  });

  it("lo recordado para OTRO padre no se usa -- la clave incluye el id del padre", () => {
    localStorage.setItem(PREFIX + "comp-otro", "cat2");
    const { result } = renderHook(() => useRememberedChild(categorias, "comp1", PREFIX));
    // No hay nada guardado para "comp1": cae en la primera de la lista, no en
    // lo que se recordó para "comp-otro".
    expect(result.current[0]).toBe("cat1");
  });

  it("sin nada recordado para este padre, usa el primer hijo de la lista", () => {
    const { result } = renderHook(() => useRememberedChild(categorias, "comp1", PREFIX));
    expect(result.current[0]).toBe("cat1");
  });

  it("lo recordado que ya no existe en la lista actual se ignora, cae en el primero", () => {
    localStorage.setItem(PREFIX + "comp1", "cat-borrada");
    const { result } = renderHook(() => useRememberedChild(categorias, "comp1", PREFIX));
    expect(result.current[0]).toBe("cat1");
  });

  it("una lista de hijos vacía da cadena vacía, no undefined", () => {
    const { result } = renderHook(() => useRememberedChild([], "comp1", PREFIX));
    expect(result.current[0]).toBe("");
  });

  it("sin scopeKey (padre sin elegir todavía), queda en cadena vacía sin intentar resolver", () => {
    const { result } = renderHook(() => useRememberedChild(categorias, "", PREFIX));
    expect(result.current[0]).toBe("");
  });

  it("mientras items es undefined (todavía cargando), no resuelve nada", () => {
    const { result } = renderHook(() => useRememberedChild(undefined, "comp1", PREFIX));
    expect(result.current[0]).toBe("");
  });
});

describe("se reresuelve solo cuando cambia el padre (scopeKey)", () => {
  it("con el mismo padre, un cambio posterior en la lista de hijos no vuelve a recalcular", () => {
    const { result, rerender } = renderHook(({ items }) => useRememberedChild(items, "comp1", PREFIX), {
      initialProps: { items: categorias },
    });
    expect(result.current[0]).toBe("cat1");

    act(() => result.current[1]("cat2")); // elección manual dentro de este padre

    rerender({ items: [{ id: "cat3" }, ...categorias] }); // la lista cambia (p. ej. SWR revalida)
    expect(result.current[0]).toBe("cat2"); // la elección manual no se pisa
  });

  it("cambiar de padre SÍ dispara una nueva resolución, para el nuevo padre", () => {
    localStorage.setItem(PREFIX + "comp2", "cat2");
    const { result, rerender } = renderHook(({ scopeKey }) => useRememberedChild(categorias, scopeKey, PREFIX), {
      initialProps: { scopeKey: "comp1" },
    });
    expect(result.current[0]).toBe("cat1");

    rerender({ scopeKey: "comp2" });
    expect(result.current[0]).toBe("cat2"); // lo recordado para el nuevo padre
  });

  it("limpia la selección cuando el padre queda vacío, y la vuelve a resolver (desde localStorage) si se reelige el mismo padre", () => {
    const { result, rerender } = renderHook(({ scopeKey }) => useRememberedChild(categorias, scopeKey, PREFIX), {
      initialProps: { scopeKey: "comp1" },
    });
    act(() => result.current[1]("cat2")); // elección manual, persistida bajo "comp1"
    expect(result.current[0]).toBe("cat2");

    rerender({ scopeKey: "" }); // el padre se deselecciona (p. ej. al cambiar de competencia)
    expect(result.current[0]).toBe(""); // no queda "cat2" pegado en pantalla mientras no hay padre

    rerender({ scopeKey: "comp1" }); // se vuelve a elegir el mismo padre
    // resolvedFor.current se limpió al deseleccionar, así que esto SÍ vuelve
    // a resolver -- y lo que encuentra en localStorage sigue siendo "cat2".
    expect(result.current[0]).toBe("cat2");
  });
});

describe("setSelectedId", () => {
  it("persiste la elección bajo storagePrefix + scopeKey", () => {
    const { result } = renderHook(() => useRememberedChild(categorias, "comp1", PREFIX));
    act(() => result.current[1]("cat2"));
    expect(localStorage.getItem(PREFIX + "comp1")).toBe("cat2");
  });

  it("sin scopeKey, actualiza el estado mostrado pero no persiste nada (no hay padre al que atar la clave)", () => {
    const { result } = renderHook(() => useRememberedChild(categorias, "", PREFIX));
    act(() => result.current[1]("cat1"));
    expect(result.current[0]).toBe("cat1");
    expect(localStorage.length).toBe(0);
  });

  it("elegir cadena vacía borra lo guardado para ese padre", () => {
    localStorage.setItem(PREFIX + "comp1", "cat2");
    const { result } = renderHook(() => useRememberedChild(categorias, "comp1", PREFIX));
    act(() => result.current[1](""));
    expect(localStorage.getItem(PREFIX + "comp1")).toBeNull();
  });

  it("si localStorage no está disponible, no revienta", () => {
    const espia = vi.spyOn(window.localStorage, "setItem").mockImplementation(() => {
      throw new Error("bloqueado");
    });
    const { result } = renderHook(() => useRememberedChild(categorias, "comp1", PREFIX));
    expect(() => act(() => result.current[1]("cat2"))).not.toThrow();
    expect(result.current[0]).toBe("cat2");
    espia.mockRestore();
  });
});

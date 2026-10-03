import { describe, expect, it } from "vitest";
import { scopeFromCascade, isScopeComplete, neededCascadeLevels } from "src/pages/lists/list-scope";

const categoriaRequerida = [{ name: "categoryId", kind: "category", required: true }];
const equipoRequerido = [{ name: "teamId", kind: "team", required: true }];
const puestoRequerido = [{ name: "position", kind: "position", required: true }];
const busquedaOpcional = [{ name: "search", kind: "search", required: false }];
const generoOpcional = [{ name: "gender", kind: "gender", required: false }];

const SIN_NIVELES = { team: false, position: false, search: false, gender: false };

function cascade({ compId = "", catId = "", teamId = "", position = "", search = "", gender = "" } = {}) {
  return { compId, catId, teamId, position, search, gender };
}

describe("scopeFromCascade", () => {
  it("resuelve cada parámetro por su kind, no por su nombre", () => {
    const scope = scopeFromCascade(categoriaRequerida, cascade({ catId: "cat-1" }));
    expect(scope).toEqual({ categoryId: "cat-1" });
  });

  it("un parámetro de equipo lee cascade.teamId", () => {
    const scope = scopeFromCascade(equipoRequerido, cascade({ teamId: "team-1" }));
    expect(scope).toEqual({ teamId: "team-1" });
  });

  it("un parámetro de puesto lee position, que no sale del cascada sino del estado propio de la página", () => {
    const scope = scopeFromCascade(puestoRequerido, cascade({ position: "2" }));
    expect(scope).toEqual({ position: "2" });
  });

  it("un parámetro de búsqueda lee search", () => {
    const scope = scopeFromCascade(busquedaOpcional, cascade({ search: "diaz" }));
    expect(scope).toEqual({ search: "diaz" });
  });

  it("un parámetro de género lee gender", () => {
    const scope = scopeFromCascade(generoOpcional, cascade({ gender: "F" }));
    expect(scope).toEqual({ gender: "F" });
  });

  it("un valor no elegido todavía queda undefined, no una cadena vacía", () => {
    const scope = scopeFromCascade(categoriaRequerida, cascade());
    expect(scope.categoryId).toBeUndefined();
  });

  it("un kind que esta pagina todavia no sabe renderizar no rompe nada", () => {
    const scope = scopeFromCascade([{ name: "metricCode", kind: "metric", required: false }], cascade());
    expect(scope).toEqual({});
  });

  it("sin parámetros (o undefined) arma un alcance vacío", () => {
    expect(scopeFromCascade([], cascade())).toEqual({});
    expect(scopeFromCascade(undefined, cascade())).toEqual({});
  });
});

describe("isScopeComplete", () => {
  it("true cuando todo parámetro requerido tiene valor", () => {
    expect(isScopeComplete(categoriaRequerida, { categoryId: "cat-1" })).toBe(true);
  });

  it("false cuando falta un parámetro requerido", () => {
    expect(isScopeComplete(categoriaRequerida, {})).toBe(false);
  });

  it("un parámetro no requerido ausente no bloquea", () => {
    expect(isScopeComplete(busquedaOpcional, {})).toBe(true);
  });

  it("sin parámetros, siempre está completo", () => {
    expect(isScopeComplete([], {})).toBe(true);
    expect(isScopeComplete(undefined, undefined)).toBe(true);
  });
});

describe("neededCascadeLevels", () => {
  it("un parámetro de categoría no necesita ningún nivel propio: competición y categoría ya se eligen antes que la lista", () => {
    expect(neededCascadeLevels(categoriaRequerida)).toEqual(SIN_NIVELES);
  });

  it("un parámetro de equipo necesita el nivel de equipo", () => {
    expect(neededCascadeLevels(equipoRequerido)).toEqual({ ...SIN_NIVELES, team: true });
  });

  it("un parámetro de puesto no necesita ningún nivel del cascada por sí solo", () => {
    expect(neededCascadeLevels(puestoRequerido)).toEqual({ ...SIN_NIVELES, position: true });
  });

  it("un parámetro de búsqueda no necesita ningún nivel del cascada por sí solo", () => {
    expect(neededCascadeLevels(busquedaOpcional)).toEqual({ ...SIN_NIVELES, search: true });
  });

  it("un parámetro de género no necesita ningún nivel del cascada por sí solo", () => {
    expect(neededCascadeLevels(generoOpcional)).toEqual({ ...SIN_NIVELES, gender: true });
  });

  it("sin parámetros, nada hace falta", () => {
    expect(neededCascadeLevels([])).toEqual(SIN_NIVELES);
    expect(neededCascadeLevels(undefined)).toEqual(SIN_NIVELES);
  });
});

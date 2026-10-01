// Arma el alcance (los query params) que un proveedor de lista necesita, a
// partir de lo que el cascada de filtros (useCascade) y los campos propios
// de esta página (puesto, búsqueda, género) tienen elegido -- y decide si ya
// hay lo suficiente como para pedir esa lista.
//
// Cada proveedor declara sus propios parámetros en el catálogo (GET /lists),
// y esta página no sabe de antemano cuáles van a ser ni cuántos: lee
// `parameters` y arma el alcance por `kind` (categoría, equipo, puesto...) en
// vez de por nombre, lo mismo que el propio backend hace con
// ListParameterKind. `source` es el cascada de useCascade extendido con lo
// que esta página agrega por su cuenta -- hoy, `{ ...cascade, position,
// search, gender }`.

const SOURCE_VALUE_BY_KIND = {
  competition: (source) => source.compId,
  category: (source) => source.catId,
  team: (source) => source.teamId,
  position: (source) => source.position,
  search: (source) => source.search,
  gender: (source) => source.gender,
};

// Los kinds que todavía no tienen un control propio en esta página (metric,
// date_range) simplemente no aportan nada al alcance -- un parámetro
// requerido de ese tipo deja la lista sin poder pedirse, en vez de romper
// esta pantalla con un control que no existe.
export function scopeFromCascade(parameters, source) {
  const scope = {};

  for (const param of parameters || []) {
    const resolve = SOURCE_VALUE_BY_KIND[param.kind];
    if (resolve) scope[param.name] = resolve(source) || undefined;
  }

  return scope;
}

export function isScopeComplete(parameters, scope) {
  return (parameters || []).every((param) => !param.required || Boolean(scope?.[param.name]));
}

// Qué controles de filtro hace falta mostrar para la lista elegida: los
// niveles del cascada (Competicion → Categoria → Equipo) más los campos
// propios de esta página (puesto, búsqueda, género). Categoria y equipo no
// tienen sentido sin competicion elegida primero -- el propio useCascade ya
// resuelve esa dependencia -- así que pedir un equipo implica mostrar los
// tres niveles, no solo el último. Los demás no dependen de nada: se
// muestran exactamente cuando la lista elegida los declara.
export function neededCascadeLevels(parameters) {
  const kinds = new Set((parameters || []).map((param) => param.kind));

  return {
    competition: kinds.has('competition') || kinds.has('category') || kinds.has('team'),
    category: kinds.has('category') || kinds.has('team'),
    team: kinds.has('team'),
    position: kinds.has('position'),
    search: kinds.has('search'),
    gender: kinds.has('gender'),
  };
}

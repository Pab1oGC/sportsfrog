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

// Qué controles de filtro hace falta mostrar para la lista elegida, aparte
// de Competicion y Categoria -- esas dos ya se eligen antes que la lista
// misma (ver ListsPage), tanto para filtrar el catálogo por el deporte de la
// competicion elegida como para tenerlas resueltas de antemano, así que no
// hace falta volver a decidir si mostrarlas. Equipo sí sigue siendo propio
// de la lista elegida: solo "plantel" lo pide, y para entonces la categoria
// ya está elegida -- useCascade ya resuelve esa dependencia. Los demás no
// dependen de nada: se muestran exactamente cuando la lista elegida los
// declara.
export function neededCascadeLevels(parameters) {
  const kinds = new Set((parameters || []).map((param) => param.kind));

  return {
    team: kinds.has('team'),
    position: kinds.has('position'),
    search: kinds.has('search'),
    gender: kinds.has('gender'),
  };
}

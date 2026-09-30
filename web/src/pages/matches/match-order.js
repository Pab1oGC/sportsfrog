import { PENDIENTE } from 'src/lib/match-status';

/**
 * El orden de la grilla de Fixtures/Partidos. Extraído de matches-page.jsx
 * (el comparador iba inline dentro de un `.sort()`) porque es puro -- sin
 * estado, sin red, cuatro niveles de comparación con reglas propias en cada
 * uno -- y es la lógica con más ramas de toda la página.
 *
 * Espera partidos ya "aumentados" con `groupLabel` (ver matches-page.jsx,
 * que lo arma por equipo local antes de ordenar) -- este módulo no lo
 * calcula, solo lo lee.
 */
export function compararPartidos(a, b) {
  // Lo que ya se jugo (o se cancelo, o se otorgo) no va a cambiar mas:
  // deja de ser lo primero que alguien necesita ver en esta pantalla, asi
  // que se hunde al fondo entero, sin importar fase, jornada o grupo. En
  // curso cuenta como pendiente — es lo mas urgente de todo.
  const pendA = PENDIENTE[a.status] ? 0 : 1;
  const pendB = PENDIENTE[b.status] ? 0 : 1;
  if (pendA !== pendB) return pendA - pendB;

  // Entre los pendientes, la jornada que sigue va primero (1, 2, 3...) y
  // el mismo sentido pone a la fase de grupos antes que la eliminatoria
  // (se juega primero). Entre los ya decididos vale lo contrario en los
  // dos niveles: lo que se jugo mas recientemente queda arriba de ese
  // bloque y lo mas viejo se sigue hundiendo — la eliminatoria (lo
  // ultimo en jugarse) por delante de los grupos ya decididos, y dentro
  // de cada una la ronda mas alta por delante de la anterior (jornada 3
  // recien terminada por encima de la 2, que a su vez tapa a la 1). Un
  // mismo signo sirve para los dos sentidos: en pendientes suma, en
  // decididos resta.
  const signo = pendA === 0 ? 1 : -1;

  // La fase manda antes que el grupo: un partido de eliminatoria trae el
  // groupLabel del equipo (que sigue siendo el de la fase de grupos, ese
  // dato no se borra al promover), y ordenar por grupo primero lo
  // mezclaba entre los partidos de esa misma zona en vez de dejarlo
  // despues de que termina toda la fase de grupos. Es null en toda la
  // fase de grupos y no-null en toda la eliminatoria, igual que ya hace
  // ReadMatches.Ordered del lado del backend y el calendario publico
  // (los dos, sin embargo, solo para el orden entre pendientes).
  const faseA = a.phase ? 1 : 0;
  const faseB = b.phase ? 1 : 0;
  if (faseA !== faseB) return signo * (faseA - faseB);

  // Dentro de la fase de grupos, la jornada manda: se lee como un
  // calendario ("que se juega esta semana", en todos los grupos a la
  // vez), no zona por zona. El grupo solo desempata partidos de la
  // misma jornada, para que ahi al menos queden juntos.
  if (!a.phase) {
    const r = signo * ((a.roundNumber || 0) - (b.roundNumber || 0));
    if (r !== 0) return r;
    return (a.groupLabel || '').localeCompare(b.groupLabel || '');
  }

  return signo * ((a.roundNumber || 0) - (b.roundNumber || 0));
}

/**
 * Los bombos de una categoría, para el sorteo de fase de grupos de
 * DrawDialog. Se cargan en Equipos, en otro momento -- y por eso es fácil
 * que un número quede viejo o suelto; esto se muestra junto a la cantidad de
 * grupos elegida para que sea una sola decisión y no dos separadas por días.
 */
export function calcularBombos({ teams, formato, groupCount, allowSamePot }) {
  const listaEquipos = teams || [];

  // Fase de grupos sin ningún equipo todavía sorteado en un grupo: "Sortear"
  // tiene que empezar por ahí, no fallar pidiendo algo que el propio sorteo
  // debería resolver.
  const sinGrupos = formato === 'groups' && listaEquipos.length > 0 && !listaEquipos.some((t) => t.groupLabel);

  const bombos = {};
  listaEquipos.forEach((t) => {
    const clave = t.seed != null ? t.seed : 'sin';
    (bombos[clave] = bombos[clave] || []).push(t.name);
  });
  const bombosNumerados = Object.keys(bombos).filter((k) => k !== 'sin').map(Number).sort((a, b) => a - b);
  const gruposElegidos = Number(groupCount) || 0;

  // Si se permite que un mismo bombo se enfrente, esa restricción deja de
  // aplicar del todo: no hay promesa que un bombo grande pueda incumplir.
  const bomboExcedido = !allowSamePot && gruposElegidos > 0 && bombosNumerados.some((b) => bombos[b].length > gruposElegidos);

  return { sinGrupos, bombos, bombosNumerados, gruposElegidos, bomboExcedido };
}

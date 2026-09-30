// Cuántos años cumplidos tiene alguien hoy, a partir de su fecha de
// nacimiento. Vivía duplicada, con el mismo algoritmo letra por letra salvo
// los nombres de variable, en athletes-page.jsx (el aviso de menor de edad)
// y reports-page.jsx (el reporte en PDF) -- consolidada acá para que un
// cambio futuro (por ejemplo, el criterio de "menor de edad" en sí) no
// tenga que hacerse dos veces ni arriesgue desalinearse.
//
// null sin fecha de nacimiento -- nunca 0: una fecha vacía no es un recién
// nacido, es un dato que todavía no se cargó.
export function edad(fechaNacimiento) {
  if (!fechaNacimiento) return null;
  const nacimiento = new Date(fechaNacimiento);
  const hoy = new Date();
  let años = hoy.getFullYear() - nacimiento.getFullYear();
  const aunNoCumple = hoy.getMonth() < nacimiento.getMonth()
    || (hoy.getMonth() === nacimiento.getMonth() && hoy.getDate() < nacimiento.getDate());
  if (aunNoCumple) años -= 1;
  return años;
}

/**
 * La hora de inicio de una jornada es de reloj de pared, sin zona: el
 * servidor la guarda como un instante, así que necesita saber a qué reloj
 * pertenece. Sin esto se leía como UTC y el partido aparecía horas antes en
 * la propia pantalla donde se escribió. Se calcula para el día elegido (no
 * "ahora") para respetar el horario de verano de esa fecha en particular --
 * `dia` ya viene resuelto por quien llama (hoy, si no se eligió ninguno).
 *
 * null sin hora de inicio -- no hay nada que anclar todavía.
 */
export function calcularUtcOffsetMinutes(dia, horaInicio) {
  if (!horaInicio) return null;
  const offset = -new Date(`${dia}T${horaInicio}`).getTimezoneOffset();
  return Number.isFinite(offset) ? offset : null;
}

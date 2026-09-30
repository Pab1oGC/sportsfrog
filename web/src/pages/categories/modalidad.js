/**
 * Un deporte de equipo tiene tres niveles -- club, equipo, jugadores -- y
 * "Max. nómina" pregunta por el último: cuántos jugadores entran en el
 * plantel. Un deporte individual, para quien lo usa, tiene dos: el club y
 * sus deportistas. La unidad que compite (una persona, o la pareja de
 * Poomsae) es una capa del modelo que el usuario no tiene por qué conocer,
 * así que preguntarle "cuántos entran en la nómina" lo hace adivinar sobre
 * un nivel que en su cabeza no existe -- y lo más probable es que conteste
 * pensando en cuántos deportistas puede anotar el club, que es otra cosa.
 *
 * Misma columna por debajo (Category.MaxRosterSize, que RosterPolicy ya hace
 * cumplir), otra pregunta arriba: cómo se compite esta categoría.
 */
export const MODALIDADES = [
  { value: '1', label: 'Individual' },
  { value: '2', label: 'Pareja' },
  { value: '3', label: 'Trío' },
];

export const modalidadDe = (value) => MODALIDADES.find((m) => m.value === String(value))?.label;

/**
 * El deporte pone el techo (Sport.MaxEntrySize) y de ahí para abajo se puede
 * elegir cualquiera: en Kyorugi la única opción es Individual, así que no
 * hay forma de inventar una dupla que la categoría no puede tener. Sin techo
 * declarado se ofrecen las tres y manda lo que diga la categoría.
 */
export function modalidadesDisponibles(maxEntrySize) {
  return maxEntrySize != null ? MODALIDADES.filter((m) => Number(m.value) <= maxEntrySize) : MODALIDADES;
}

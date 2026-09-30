/**
 * Cuánto cabe en una inscripción, y cómo se llama la pantalla entera según
 * eso -- decide el título, la etiqueta del botón, el encabezado de columna,
 * el mensaje de borrado y el `femenino` de EstadoChip en teams-page.jsx.
 *
 * Cupo sale de dos lugares, y manda el menor: el techo del deporte
 * (Sport.MaxEntrySize -- Kyorugi 1, Poomsae 3) y el cupo de la categoría, que
 * puede achicarlo (Poomsae individual dentro del techo de trío) pero nunca
 * ensancharlo. Es la misma cuenta que hace RosterPolicy del lado del
 * servidor, que es quien de verdad lo hace cumplir.
 *
 * "Equipo" es el nombre correcto del lado del backend (ver el comentario de
 * Team.cs: "la unidad que compite", no un sinónimo de club) pero desde el
 * frontend eso se lee raro en un deporte individual: para el usuario, una
 * inscripción de una sola persona *es* el deportista, y llamarla equipo -- o
 * incluso "inscripción" -- es hablarle de una capa interna que no le
 * importa. El backend no se entera de este cambio de vocabulario: mismos
 * endpoints, mismo contrato, mismo Team por debajo.
 */
export function derivarVocabulario({ individual, sport, categoria }) {
  const cupos = [sport?.maxEntrySize, categoria?.maxRosterSize].filter((v) => v != null);
  const cupo = cupos.length > 0 ? Math.min(...cupos) : 1;
  const soloDeportista = individual && cupo === 1;

  const entityName = soloDeportista ? 'deportista' : (individual ? 'inscripción' : 'equipo');
  const entityGender = individual && !soloDeportista ? 'f' : 'm';

  return { cupo, soloDeportista, entityName, entityGender };
}

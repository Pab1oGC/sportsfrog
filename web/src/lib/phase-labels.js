// Como se llama cada fase de una eliminatoria, en el mismo texto que ya
// usaba el portal público — para que la fase de un partido se lea igual
// tanto en el panel interno como en la página pública, y ninguno de los dos
// muestre el código en crudo que manda el backend (Bracket.Phase). Las
// claves son las que Bracket.Phase() realmente devuelve (ver sus propios
// tests) -- las anteriores (round_of_32, round_of_16, quarterfinal,
// third_place) no matcheaban ninguna fase real: el backend nunca las manda,
// asi que la columna "Fase" caia siempre al codigo en crudo salvo para
// "final" y "semifinal", que coincidian de casualidad.
export const FASES = {
  dieciseisavos: 'Dieciseisavos',
  octavos: 'Octavos de final',
  cuartos: 'Cuartos de final',
  semifinal: 'Semifinales',
  final: 'Final',
};

// Mas alla de dieciseisavos, Bracket.Phase() no tiene nombre fijo -- devuelve
// "ronda N" con el numero que sea, asi que ninguna tabla estatica lo va a
// tener nunca cargado. Capitalizar la primera letra ahi (y en cualquier otro
// codigo que este mapa todavia no conozca) es lo unico que se puede
// garantizar sin inventar un nombre.
const conMayuscula = (texto) => texto.charAt(0).toUpperCase() + texto.slice(1);

export const nombreFase = (codigo) => FASES[codigo] || conMayuscula(codigo);

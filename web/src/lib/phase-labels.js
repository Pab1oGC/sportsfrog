// Como se llama cada fase de una eliminatoria, en el mismo texto que ya
// usaba el portal público — para que la fase de un partido se lea igual
// tanto en el panel interno como en la página pública, y ninguno de los dos
// muestre el código en crudo que manda el backend (Bracket.Phase).
export const FASES = {
  round_of_32: 'Dieciseisavos',
  round_of_16: 'Octavos',
  quarterfinal: 'Cuartos de final',
  semifinal: 'Semifinales',
  final: 'Final',
  third_place: 'Tercer puesto',
};

export const nombreFase = (codigo) => FASES[codigo] || codigo;

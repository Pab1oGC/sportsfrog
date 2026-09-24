// Un <input type="number"> deja tipear "-", "+", "e" y "." aunque tenga min=0
// (min solo limita las flechitas y la validacion del formulario), y tambien
// pegar un negativo. Un minuto, una cantidad, una ronda o un marcador son
// enteros sin signo: se bloquea la tecla con `bloquearNoEnteros` (onKeyDown)
// y `soloDigitos` limpia lo que llegue por pegado (onChange).
//
//   <TextField type="number" value={v}
//     onChange={(e) => setV(soloDigitos(e.target.value))}
//     onKeyDown={bloquearNoEnteros}
//     slotProps={{ htmlInput: { min: 0, step: 1 } }} />
//
// Un peso o un puntaje de jueces tambien es positivo pero lleva decimales:
// `bloquearNegativos` + `soloDecimales` hacen lo mismo dejando pasar el punto.

const TECLAS_NO_ENTERAS = ['-', '+', 'e', 'E', '.', ','];
const TECLAS_NEGATIVAS = ['-', '+', 'e', 'E'];

export function bloquearNoEnteros(e) {
  if (TECLAS_NO_ENTERAS.includes(e.key)) e.preventDefault();
}

export function soloDigitos(texto) {
  return String(texto).replace(/\D/g, '');
}

export function bloquearNegativos(e) {
  if (TECLAS_NEGATIVAS.includes(e.key)) e.preventDefault();
}

export function soloDecimales(texto) {
  return String(texto).replace(/[^\d.]/g, '');
}

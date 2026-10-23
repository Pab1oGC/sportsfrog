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
// Eso alcanza mientras el campo nunca pasa por cero. En cuanto lo hace --
// se borra del todo y el estado cae a 0, o simplemente se escribe un 0 a
// proposito -- aparece un bug distinto: React no resincroniza el value de
// un <input type="number"> mientras esta enfocado (para no interrumpir
// mientras alguien termina de escribir "1." o "-5"), asi que ese cero se
// queda pegado en pantalla hasta perder el foco -- se escribe "1" a
// continuacion y se ve "01". `limpiarEntero` es `soloDigitos` mas la
// correccion para ese caso: si el cero inicial sobra, lo saca a mano del
// nodo del DOM antes de devolver el texto limpio -- eso si toma efecto de
// inmediato, porque no pasa por la reconciliacion de React. Reemplaza al
// par soloDigitos/onChange de arriba entero (ya incluye el filtrado):
//
//   <TextField type="number" value={v ?? ''}
//     onChange={(e) => { const limpio = limpiarEntero(e); setV(limpio === '' ? null : +limpio); }}
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

export function limpiarEntero(e) {
  const limpio = soloDigitos(e.target.value).replace(/^0+(?=\d)/, '');
  // Mutado solo si cambio: tocar e.target.value sin necesidad reposiciona
  // el cursor en algunos navegadores, y la mayoria de los tipeos (ningun
  // cero inicial de por medio) no lo necesitan.
  if (limpio !== e.target.value) e.target.value = limpio;
  return limpio;
}

export function bloquearNegativos(e) {
  if (TECLAS_NEGATIVAS.includes(e.key)) e.preventDefault();
}

export function soloDecimales(texto) {
  return String(texto).replace(/[^\d.]/g, '');
}

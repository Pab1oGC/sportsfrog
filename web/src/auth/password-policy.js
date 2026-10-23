/**
 * Espejo en el cliente de la politica de contraseñas del dominio
 * (Password.cs: largo minimo, mayuscula, minuscula, digito y un caracter
 * especial).
 *
 * El servidor sigue siendo el que decide -- esto no lo reemplaza, lo
 * adelanta: sin esto la unica forma de saber que una contraseña no alcanza
 * es mandarla y leer el 400. Que las dos definiciones puedan separarse es
 * real, y es el motivo de que esto este en un solo archivo y no repetido en
 * cada formulario.
 *
 * Las clases de caracteres son las de Unicode, no [A-Z]/[a-z]/[0-9], porque
 * char.IsUpper y char.IsLetterOrDigit del lado del servidor tampoco se
 * limitan al ASCII.
 */
export const LARGO_MINIMO = 8;

/** Lo mismo que dice la API cuando rechaza una contraseña. */
export const MENSAJE_POLITICA =
  `La contraseña debe tener al menos ${LARGO_MINIMO} caracteres e incluir una mayúscula, ` +
  'una minúscula, un número y un carácter especial.';

// Un espacio es la ausencia de un caracter especial, no uno de ellos -- la
// misma aclaracion que Password.IsSpecial lleva escrita: sin excluir el
// espacio en blanco, "no es letra ni digito" lo aceptaria y debilitaria la
// politica sin que se note.
const ESPECIAL = /[^\p{L}\p{N}\s]/u;

export function cumplePolitica(valor) {
  if (typeof valor !== 'string' || valor.length < LARGO_MINIMO) return false;

  return /\p{Lu}/u.test(valor)
    && /\p{Ll}/u.test(valor)
    && /\p{Nd}/u.test(valor)
    && ESPECIAL.test(valor);
}

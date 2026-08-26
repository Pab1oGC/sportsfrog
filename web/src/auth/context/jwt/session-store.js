/* ---------------------------------------------------------------------------
   Donde vive el token de renovacion.

   Por defecto en memoria, y esa es toda la decision: dura 30 dias del lado
   del servidor, y guardado en localStorage sobrevive a cerrar el navegador.
   En la computadora de la oficina de la liga eso significa que el siguiente
   que se sienta abre las herramientas de desarrollo, lo copia, y tiene un mes
   de esa sesion desde su propia maquina. No hace falta vulnerar nada.

   En memoria desaparece al cerrar la pestana, que es exactamente lo que uno
   espera de una sesion que no pidio recordar.

   Con "recordarme" tildado si se persiste, porque entonces es una decision
   tomada a proposito y no un valor por omision que nadie eligio.
   --------------------------------------------------------------------------- */

var CLAVE = 'refresh_token';
var CLAVE_RECORDAR = 'remember_session';

/**
 * El token cuando la sesion no se recuerda.
 *
 * Una variable de modulo: no la lee otra pestana, no sobrevive a una recarga
 * y no queda en ningun almacenamiento que se pueda inspeccionar despues.
 */
var enMemoria = null;

export function guardarRenovacion(token, recordar) {
  enMemoria = token || null;

  try {
    if (recordar && token) {
      localStorage.setItem(CLAVE, token);
      localStorage.setItem(CLAVE_RECORDAR, '1');
    } else {
      // Se limpia igual: alguien que antes eligio ser recordado y ahora no,
      // no puede quedarse con el token viejo dando vueltas.
      localStorage.removeItem(CLAVE);
      localStorage.removeItem(CLAVE_RECORDAR);
    }
  } catch (e) {
    // Modo privado, o almacenamiento bloqueado. La sesion sigue en memoria.
  }
}

export function leerRenovacion() {
  if (enMemoria) return enMemoria;
  try {
    return localStorage.getItem(CLAVE);
  } catch (e) {
    return null;
  }
}

export function seRecuerda() {
  try {
    return localStorage.getItem(CLAVE_RECORDAR) === '1';
  } catch (e) {
    return false;
  }
}

export function olvidarRenovacion() {
  enMemoria = null;
  try {
    localStorage.removeItem(CLAVE);
    localStorage.removeItem(CLAVE_RECORDAR);
  } catch (e) {
    // Nada que hacer: si no se puede escribir, tampoco se escribio antes.
  }
}

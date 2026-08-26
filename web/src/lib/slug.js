/* -------------------------------------------------------------------------
   Direcciones publicas (slugs).

   Espejo de Slug.cs en la API. Se replica a proposito y con esa deuda
   asumida: la API es la que manda y vuelve a validar, pero avisar recien al
   apretar Guardar es lo que hace que alguien escriba "Copa Apertura 2027",
   no vea nada y crea que el boton esta roto.
   ------------------------------------------------------------------------- */

export var SLUG_MIN = 3;
export var SLUG_MAX = 63;

var FORMA = /^[a-z0-9]+(-[a-z0-9]+)*$/;

/**
 * Convierte un nombre en una direccion utilizable.
 *
 * Los acentos se descomponen y se les quita la tilde en lugar de reemplazarse
 * por un guion: "Relámpago" tiene que dar "relampago", no "rel-mpago". La ñ
 * cae por el mismo camino y termina en n.
 */
export function aSlug(texto) {
  return (texto || '')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, SLUG_MAX)
    .replace(/-+$/, '');
}

/**
 * Lo que la API hace antes de validar: recorta y baja a minusculas.
 *
 * Escribirlo en mayusculas no es un error, es la misma direccion, y asi se
 * acepta en vez de rechazarse.
 */
export function normalizarSlug(texto) {
  return (texto || '').trim().toLowerCase();
}

/**
 * El motivo por el que la API la rechazaria, o null si esta bien.
 *
 * Devuelve un motivo concreto en lugar de un booleano porque "invalido" no le
 * dice a nadie que fue el espacio.
 */
export function problemaDeSlug(texto) {
  var s = normalizarSlug(texto);

  if (!s) return 'La direccion es obligatoria.';
  if (s.length < SLUG_MIN) return 'Necesita al menos ' + SLUG_MIN + ' caracteres.';
  if (s.length > SLUG_MAX) return 'No puede pasar de ' + SLUG_MAX + ' caracteres.';

  if (!FORMA.test(s)) {
    return 'Solo minusculas, numeros y guiones simples entre ellos: '
      + 'sin espacios, acentos, ñ, ni guion al principio o al final.';
  }

  return null;
}

/** El slug de la organizacion activa, para poder mostrar la direccion entera. */
export function slugDeOrganizacion() {
  try {
    var orgs = JSON.parse(localStorage.getItem('organizations') || '[]');
    return (orgs[0] && orgs[0].slug) || 'tu-liga';
  } catch (e) {
    return 'tu-liga';
  }
}

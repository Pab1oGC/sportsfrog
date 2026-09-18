// Coordenadas leidas de un enlace de Google Maps, en cualquiera de las
// formas en que Maps las entrega:
//   - "!3d<lat>!4d<lng>" -- las del pin exacto, en un enlace de "lugar"
//     (.../maps/place/Nombre/@.../data=!...!3d-17.39!4d-66.15). Es la mas
//     confiable de las cuatro: a diferencia de "@lat,lng" no es donde quedo
//     centrado el mapa, es el punto que Google marco.
//   - "q=lat,lng" -- lo que arma el selector de mapa de una sede.
//   - "@lat,lng" -- el mapa centrado ahi cuando se comparte tras moverlo o
//     hacer zoom; puede no coincidir con el pin si se movio el mapa antes
//     de copiar el enlace.
//   - "ll=lat,lng".
// Devuelve null si el enlace no trae ninguna -- un lugar por nombre, un
// enlace corto (maps.app.goo.gl/...) sin resolver -- que es el caso comun
// para un enlace pegado a mano en vez de generado por el selector.
export function parseLatLng(url) {
  if (!url) return null;

  // Los enlaces de "lugar" traen la coma de "!3d...!4d..." tal cual, pero
  // los que arma manualmente alguien copiando una URL con espacios raros a
  // veces llegan con la coma codificada -- decodificar antes de buscar cubre
  // ambos sin duplicar los patrones.
  var texto = url;
  try { texto = decodeURIComponent(url); } catch { /* enlace ya decodificado, o invalido -- se sigue con el original */ }

  var patrones = [
    /!3d(-?\d+\.\d+)!4d(-?\d+\.\d+)/,
    /[?&]q=(-?\d+\.\d+),(-?\d+\.\d+)/,
    /@(-?\d+\.\d+),(-?\d+\.\d+)/,
    /[?&]ll=(-?\d+\.\d+),(-?\d+\.\d+)/,
  ];

  for (var i = 0; i < patrones.length; i++) {
    var m = texto.match(patrones[i]);
    if (m) return { lat: parseFloat(m[1]), lng: parseFloat(m[2]) };
  }

  return null;
}

// Los mismos hosts que el back permite seguir en ManageVenues.ResolveMapsLinkAsync
// -- un enlace corto no trae coordenadas propias, solo un redireccionamiento
// que el navegador no puede leer entre origenes distintos, asi que hace
// falta pedirle al servidor que lo siga.
const HOSTS_DE_ENLACE_CORTO = new Set(['maps.app.goo.gl', 'goo.gl']);

export function esEnlaceCorto(url) {
  try {
    return HOSTS_DE_ENLACE_CORTO.has(new URL(url).host);
  } catch {
    return false;
  }
}

// El enlace que arma el selector de mapa al elegir un punto -- abre
// directamente en Google Maps centrado ahi, funciona pegado en cualquier
// lado.
export function linkFromLatLng(lat, lng) {
  return 'https://www.google.com/maps?q=' + lat.toFixed(6) + ',' + lng.toFixed(6);
}

// La URL que Google si deja incrustar en un iframe ajeno -- la mayoria de
// las paginas normales de Maps lo rechazan (X-Frame-Options), asi que un
// enlace pegado a mano en vez de generado por el selector rara vez funciona
// tal cual dentro de un iframe. Si se pueden leer coordenadas se arma un
// enlace de consulta limpio con output=embed (que si se deja incrustar).
//
// Si no se pueden leer -un enlace corto sin resolver, un enlace de "lugar
// por nombre" sin coordenadas visibles- null, no una adivinanza: se
// intento en algun momento pasarle a Google la URL entera como si fuera
// un texto de busqueda (?q=<url>&output=embed), y eso es precisamente lo
// que produce el error de Google "no se pudo mostrar el contenido
// personalizado" mas un mapa del mundo sin zoom -no un respaldo silencioso,
// un error visible- asi que ya no se intenta. Quien llama a esta funcion
// decide que mostrar en su lugar (ver MapaSedeDialog).
export function embedSrc(mapsUrl) {
  if (!mapsUrl) return null;
  if (mapsUrl.includes('/maps/embed')) return mapsUrl;

  var coords = parseLatLng(mapsUrl);
  return coords ? 'https://www.google.com/maps?q=' + coords.lat + ',' + coords.lng + '&output=embed' : null;
}

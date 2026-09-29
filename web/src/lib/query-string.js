// Agrega parametros de consulta a una URL sin duplicar el "?" cuando la base
// ya trae uno (por ejemplo, endpoints.athletes + "?search=..." armado a mano
// en la propia pagina) y sin mandar los que vienen null/undefined/'' -- asi
// el llamador puede pasar un objeto con huecos (un filtro sin elegir, una
// pagina sin pedir) sin tener que limpiarlo antes.
export function withQueryParams(url, params) {
  const entries = Object.entries(params || {}).filter(
    ([, value]) => value !== null && value !== undefined && value !== '',
  );

  if (entries.length === 0) return url;

  const query = new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString();

  return url.includes('?') ? `${url}&${query}` : `${url}?${query}`;
}

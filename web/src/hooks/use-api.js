import useSWR from "swr";
import axios from "src/lib/axios";

// Ambos, no solo el cuerpo: X-Total-Count (ver PagedListing en el backend)
// es lo unico que dice cuantas filas hay en total cuando un listado esta
// paginado, y solo viaja como cabecera -- el cuerpo sigue siendo el mismo
// arreglo de siempre, paginado o no. Envolver la respuesta acá adentro no
// cambia nada para quien ya usa useApi: `data` sigue siendo exactamente el
// cuerpo de la respuesta, ver el desenvuelto en useApi mas abajo.
const fetcher = async (url) => {
  const response = await axios.get(url);
  const totalCount = response.headers?.["x-total-count"];

  return { data: response.data, totalCount: totalCount != null ? Number(totalCount) : null };
};

export function useApi(url, options) {
  const { data: payload, error, isLoading, mutate } = useSWR(url, fetcher, { revalidateOnFocus: false, ...options });
  return { data: payload?.data, totalCount: payload?.totalCount ?? null, error, isLoading, mutate };
}

export async function apiPost(url, body) {
  const res = await axios.post(url, body);
  return res.data;
}

export async function apiPut(url, body) {
  const res = await axios.put(url, body);
  return res.data;
}

export async function apiDelete(url) {
  const res = await axios.delete(url);
  return res.data;
}

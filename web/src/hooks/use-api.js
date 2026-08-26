import useSWR from "swr";
import axios from "src/lib/axios";

const fetcher = (url) => axios.get(url).then((r) => r.data);

export function useApi(url, options) {
  const { data, error, isLoading, mutate } = useSWR(url, fetcher, { revalidateOnFocus: false, ...options });
  return { data, error, isLoading, mutate };
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

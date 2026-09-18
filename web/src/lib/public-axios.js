import axios from 'axios';

// Axios instance without auth headers — used by public portal pages.
const publicAxios = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '',
  headers: { 'Content-Type': 'application/json' },
});

publicAxios.interceptors.response.use(
  (r) => r,
  (error) => {
    const msg = error?.response?.data?.detail || error?.message || 'Error';
    return Promise.reject(new Error(msg));
  },
);

export default publicAxios;

// Compartido por cada pantalla que arma su propio useSWR contra la API
// publica (landing-page.jsx, playful-hero.jsx) -- antes duplicado como
// funcion local en cada una.
export function publicFetcher(url) {
  return publicAxios.get(url).then((r) => r.data);
}
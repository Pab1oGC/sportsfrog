import axios, { endpoints } from "src/lib/axios";
import { setSession } from "./utils";
import { guardarRenovacion, leerRenovacion, olvidarRenovacion, seRecuerda } from "./session-store";

/**
 * Guarda lo que devuelve la API tras iniciar o renovar una sesion.
 *
 * El token de renovacion rota en cada uso: la API emite uno nuevo y anula el
 * anterior. Por eso hay que reemplazarlo siempre, y por eso una copia robada
 * deja de servir apenas el cliente legitimo renueva.
 */
function establecer(datos, recordar) {
  setSession(datos.accessToken);
  guardarRenovacion(datos.refreshToken, recordar);

  try {
    localStorage.setItem("organizations", JSON.stringify(datos.organizations || []));
  } catch (e) {
    // Sin almacenamiento no hay lista de organizaciones persistida; la sesion
    // sigue viva y se vuelve a pedir al renovar.
  }

  return datos;
}

export async function signIn({ email, password, remember }) {
  const res = await axios.post(endpoints.auth.signIn, { email, password });
  return establecer(res.data, !!remember);
}

export async function signUp(data) {
  const res = await axios.post(endpoints.auth.signUp, data);
  return res.data;
}

/**
 * Cambia el token de renovacion por una sesion nueva.
 *
 * Devuelve null cuando no hay con que renovar o la API lo rechaza, y en ese
 * caso deja todo limpio: un token que ya no sirve guardado es solo algo mas
 * que alguien puede leer.
 */
export async function renewSession() {
  const token = leerRenovacion();
  if (!token) return null;

  try {
    const res = await axios.post(endpoints.auth.renew, { refreshToken: token });
    return establecer(res.data, seRecuerda());
  } catch (e) {
    await limpiar();
    return null;
  }
}

export async function signOut() {
  const token = leerRenovacion();

  if (token) {
    // Se revoca del lado del servidor antes de borrarlo de aca. Borrarlo
    // nomas lo dejaria valido 30 dias mas, sirviendole a cualquiera que ya
    // se hubiera quedado con una copia.
    try {
      await axios.post(endpoints.auth.signOut, { refreshToken: token });
    } catch (e) {
      // Sin conexion, o ya estaba revocado. Se limpia igual.
    }
  }

  await limpiar();
}

async function limpiar() {
  setSession(null);
  olvidarRenovacion();
  try {
    localStorage.removeItem("organizations");
    localStorage.removeItem("expires_in");
  } catch (e) {
    // Nada que limpiar.
  }
}

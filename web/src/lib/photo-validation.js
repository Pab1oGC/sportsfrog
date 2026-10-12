import axios, { endpoints } from 'src/lib/axios';

/**
 * Lo que la pantalla de deportistas muestra del veredicto de una foto.
 *
 * Vive aca y no en la pagina para que la grilla, el formulario y las pruebas
 * usen los mismos nombres y colores. Los estados son los que responde la API
 * (ver PhotoValidationView en el backend): approved, rejected y
 * not_evaluated.
 */
export const ESTADO_FOTO = {
  approved: { label: 'Aprobada', color: 'success' },
  rejected: { label: 'Rechazada', color: 'error' },
  not_evaluated: { label: 'Sin evaluar', color: 'default' },
};

const MENSAJE_NO_DISPONIBLE =
  'No se pudo validar la foto en este momento. Intentá de nuevo en unos minutos.';

/**
 * El estado de una foto, siempre dentro de los conocidos. Un veredicto
 * ausente o con un estado que esta pantalla no conoce se trata como "sin
 * evaluar": es lo que dice la base cuando nadie la revisó, y nunca se
 * presenta como aprobada ni rechazada por error.
 */
export function estadoDeFoto(validacion) {
  return ESTADO_FOTO[validacion?.state] ? validacion.state : 'not_evaluated';
}

/**
 * Lo que la persona tiene que leer sobre la foto, en los tres niveles que
 * distingue el validador:
 *
 * - motivos: fallo una regla critica. Bloquea: la foto no se puede guardar.
 * - advertencias: fallo una regla no critica. No bloquea: se guarda bajo la
 *   responsabilidad de quien la sube.
 * - noVerificado: reglas que no se revisaron. No son defectos.
 */
export function detalleDeFoto(validacion) {
  return {
    motivos: validacion?.reasons ?? [],
    advertencias: validacion?.warnings ?? [],
    noVerificado: validacion?.unverified ?? [],
  };
}

/**
 * Pide a la API el veredicto de una foto sin guardarla. Lanza un Error con
 * un mensaje listo para mostrar: la pantalla no tiene que conocer los
 * codigos HTTP.
 */
export async function evaluarFoto(archivo) {
  const cuerpo = new FormData();
  cuerpo.append('archivo', archivo);

  try {
    const { data } = await axios.post(endpoints.athletePhotoPreview, cuerpo);
    return data;
  } catch (error) {
    throw new Error(mensajeDeError(error));
  }
}

/** La frase para la persona, a partir de un error de la API. */
export function mensajeDeError(error) {
  const status = error?.response?.status;

  if (status === 503) return MENSAJE_NO_DISPONIBLE;

  // ValidationProblem: la API responde los errores por campo, y el campo de
  // la foto es "archivo".
  if (status === 422) {
    return error.response.data?.errors?.archivo?.[0] ?? 'La foto no se pudo leer.';
  }

  return error?.response?.data?.detail ?? error?.message ?? 'No se pudo validar la foto.';
}

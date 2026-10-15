import { endpoints } from 'src/lib/axios';

/**
 * A qué endpoint mandar la inscripción -- depende de cuántos estudiantes
 * terminaron elegidos, no de una elección explícita en el formulario.
 * Elegir más de uno pasa por EnrollStudentsBulk en vez de EnrollStudent --
 * todo o nada, misma razón que ya aplica crearUrlDeAlta para la nómina de
 * un equipo: cinco personas revisadas de a una contra un cupo de tres
 * dejarían pasar a las primeras tres y rechazarían a las últimas dos por
 * una razón que no tiene nada que ver con ellas.
 */
export function crearUrlDeInscripcion(courseId, form) {
  if (!courseId) return null;
  return form.athleteIds.length > 1
    ? endpoints.courseEnrollmentsBulk(courseId)
    : endpoints.courseEnrollments(courseId);
}

/**
 * El cuerpo que se manda al inscribir, en sus dos formas: varios de una
 * (bulk, solo los ids) o uno solo (athleteId suelto, igual que siempre).
 */
export function mapearEnvioDeInscripcion(form) {
  if (form.athleteIds.length > 1) {
    return { athleteIds: form.athleteIds };
  }
  return { athleteId: form.athleteIds[0] };
}

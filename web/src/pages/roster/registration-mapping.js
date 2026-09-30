import { endpoints } from 'src/lib/axios';

/**
 * A qué endpoint mandar el alta de nómina -- depende de cuántos deportistas
 * terminaron elegidos, no de una elección explícita en el formulario. Elegir
 * más de uno pasa por RegisterPlayersBulk en vez de RegisterPlayer -- todo o
 * nada, igual que ya promete la importación por Excel: cinco personas
 * revisadas de a una contra un cupo de tres dejarían pasar a las primeras
 * tres y rechazarían a las últimas dos por una razón que no tiene nada que
 * ver con ellas.
 */
export function crearUrlDeAlta(teamId, form) {
  if (!teamId) return null;
  return form.athleteIds.length > 1 ? endpoints.rosterRegisterBulk(teamId) : endpoints.roster(teamId);
}

/**
 * El cuerpo que se manda al guardar un registro de nómina, en sus tres
 * formas: editar uno existente, dar de alta varios de una (bulk), o dar de
 * alta uno solo.
 *
 * Al editar, CorrectRegistration ni acepta un athleteId -- "la persona no se
 * edita acá" es su propio contrato, ver el comentario del backend. El alta
 * múltiple no pide dorsal ni posición: son por persona, y no hay uno solo
 * que pedir para varios a la vez -- se cargan después, editando cada
 * registro.
 */
export function mapearEnvioDeNomina(f, wasEdit) {
  if (wasEdit) {
    return { jerseyNumber: f.jerseyNumber ? Number(f.jerseyNumber) : null, position: f.position || null };
  }
  if (f.athleteIds.length > 1) {
    return { athleteIds: f.athleteIds };
  }
  return { athleteId: f.athleteIds[0], jerseyNumber: f.jerseyNumber ? Number(f.jerseyNumber) : null, position: f.position || null };
}

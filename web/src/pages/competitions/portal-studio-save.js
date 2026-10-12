import { buildPortalPayload } from 'src/pages/competitions/portal-payload';

/**
 * El cuerpo del PUT /competitions/{id} que hace PortalStudioPage.save(). El
 * contrato es la competencia ENTERA -- no hay un PATCH parcial -- así que
 * hay que reenviar sin tocar los campos que no son del portal (nombre,
 * dirección, reglamento, calendario, boletín) y solo reescribir
 * settings.public.
 *
 * `schedule`/`bulletin` se reenvían intactos, leídos de `row` (la propia
 * competencia tal como llegó de la API), no de `form` (el estudio no tiene
 * ningún campo para ninguno de los dos). Esto es a propósito una función
 * aparte, fácil de probar sola: el PUT reemplaza el objeto `settings`
 * entero, así que una regresión acá -- alguien que "simplifica" esto para
 * armar `settings` de otra forma -- borraría en silencio el calendario o el
 * boletín de la competencia en el próximo guardado del portal, sin que
 * nada en el estudio de portal avise que eso pasó.
 */
export function buildCompetitionUpdatePayload(row, form) {
  return {
    rulesetId: row.rulesetId,
    name: row.name,
    slug: row.slug,
    season: row.season,
    format: row.format,
    captureLevel: row.captureLevel,
    startsOn: row.startsOn,
    endsOn: row.endsOn,
    // El diseño de credencial no lo edita el estudio, pero el PUT lo reescribe
    // entero: sin reenviarlo, cada guardado del portal borraría la elección.
    credentialDesignId: row.credentialDesignId || null,
    settings: {
      schedule: (row.settings && row.settings.schedule) || null,
      bulletin: (row.settings && row.settings.bulletin) || null,
      public: buildPortalPayload(form),
    },
  };
}

/**
 * Qué excepciones tiene una persona, derivado sin pedirle al backend una
 * lista de excepciones en crudo que no publica.
 *
 * ReadAthleteAccreditation devuelve lo ya resuelto -el paquete de la
 * categoría fusionado con las excepciones- no las excepciones por sí solas.
 * Lo que sí alcanza para reconstruirlas es comparar ese resultado contra el
 * paquete de la categoría, elemento por elemento:
 *
 *   · en el paquete y en lo resuelto   -> sin excepción, incluido por la categoría
 *   · en el paquete, no en lo resuelto -> excepción que quita (granted: false)
 *   · no en el paquete, en lo resuelto -> excepción que agrega (granted: true)
 *   · en ninguno de los dos            -> sin excepción, no incluido
 *
 * Separado de OverridesDialog porque es la única parte de ese diálogo que es
 * una regla y no una pantalla -- lo mismo que ya hace este proyecto con
 * modalidad.js o batch-polling.js, cada uno la lógica de una sola pantalla,
 * afuera de ella, para poder probarla sin montar nada.
 */

/**
 * @param {string[]} packageItemIds - itemIds del paquete de la categoría actual.
 * @param {string[]} resolvedItemIds - itemIds que la persona resuelve ahora mismo.
 * @returns {Record<string, boolean>} Un mapa itemId -> granted, con una
 *   entrada solo para los elementos que SÍ tienen una excepción. Un elemento
 *   sin excepción -ya sea "según categoría, incluido" o "según categoría, no
 *   incluido"- no aparece.
 */
export function deriveOverrideState(packageItemIds, resolvedItemIds) {
  const inPackage = new Set(packageItemIds || []);
  const inResolved = new Set(resolvedItemIds || []);
  const state = {};

  for (const id of new Set([...inPackage, ...inResolved])) {
    const wasInPackage = inPackage.has(id);
    const isResolved = inResolved.has(id);
    if (wasInPackage === isResolved) continue;
    state[id] = isResolved;
  }

  return state;
}

/** El estado del editor, en la forma que SetAthleteAccreditationOverrides espera. */
export function overridesToSend(state) {
  return Object.entries(state).map(([itemId, granted]) => ({ itemId, granted }));
}

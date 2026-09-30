/**
 * Por posición y después por dorsal: buscar "el defensor número 4" a ojo en
 * una lista sin ningún orden táctico era lo que hacía lenta la carga en
 * EventsDialog. Sin posición cargada queda al final, no mezclado en
 * cualquier lado.
 */
export function porPosicionYDorsal(a, b) {
  const posA = a.position || '';
  const posB = b.position || '';
  if (posA !== posB) {
    if (!posA) return 1;
    if (!posB) return -1;
    return posA.localeCompare(posB, 'es');
  }
  return (a.jerseyNumber ?? 999) - (b.jerseyNumber ?? 999);
}

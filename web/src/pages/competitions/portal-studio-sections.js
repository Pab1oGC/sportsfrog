/**
 * Las mismas secciones que "Secciones visibles" dejó prendidas, en el orden
 * elegido -- lo que la vista previa necesita para dibujar sus pestañas de a
 * mentira, en el mismo orden que verá el visitante. La galería además
 * necesita al menos una foto, igual que en el portal real.
 */
export function seccionesVisibles(form) {
  if (!form) return [];
  const visible = {
    standings: form.showStandings,
    leaders: form.showLeaders,
    classification: form.showClassification,
    calendar: true,
    gallery: form.showGallery && form.gallery.some((p) => p.key),
  };
  return form.sectionOrder.filter((s) => visible[s.key]);
}

/**
 * El nuevo orden de secciones tras subir/bajar una fila -- o el mismo
 * arreglo (misma referencia) si el movimiento se saldría de rango, para que
 * quien llama pueda notar que no hubo cambio y evitar un re-render de más.
 */
export function ordenTrasMover(sectionOrder, index, direction) {
  const target = index + direction;
  if (target < 0 || target >= sectionOrder.length) return sectionOrder;
  const next = sectionOrder.slice();
  [next[index], next[target]] = [next[target], next[index]];
  return next;
}

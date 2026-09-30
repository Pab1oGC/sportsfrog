/**
 * Dónde no se debe recortar la imagen de portada, a partir de un clic sobre
 * la propia imagen en FocalPointPicker: el clic en píxeles, convertido a un
 * porcentaje 0..100 de dónde cayó dentro del rectángulo de la imagen.
 */
export function puntoFocalDesdeClic(rect, clientX, clientY) {
  const nx = Math.round(((clientX - rect.left) / rect.width) * 100);
  const ny = Math.round(((clientY - rect.top) / rect.height) * 100);
  return {
    x: Math.max(0, Math.min(100, nx)),
    y: Math.max(0, Math.min(100, ny)),
  };
}

// Lee un archivo elegido por el usuario como data URL, la forma en la que
// una imagen viaja hacia el backend antes de tener una clave de
// almacenamiento propia. Compartido por cada pantalla que sube una foto
// (club, organizacion, portal de una competencia) para que las cuatro no
// repitan el mismo FileReader.
export function leerComoDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

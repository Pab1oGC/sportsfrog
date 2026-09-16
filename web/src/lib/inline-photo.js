// Espejo del lado del cliente de SportFrog.Api.Infrastructure.Validation.
// InlinePhoto en el backend: mismos tres formatos, mismo limite de tamano,
// medido igual (sobre el texto de la data URL, que pesa un tercio mas que
// los bytes que lleva adentro -- por eso el limite de 5 MB de texto ronda
// una imagen de 4 MB). Repetido aca a proposito: el backend sigue siendo
// quien decide si el archivo se guarda, esto solo evita un viaje de red
// para avisar de un archivo que el backend va a rechazar igual.
const PREFIX = 'data:image/';
const SUPPORTED_TYPES = ['image/png', 'image/jpeg', 'image/webp'];
const MAX_LENGTH = 5 * 1024 * 1024;

export const INLINE_PHOTO_REQUIREMENT =
  `La foto tiene que ser PNG, JPEG o WebP, de menos de ${MAX_LENGTH / (1024 * 1024)} MB.`;

/**
 * Lee un archivo elegido por la persona y lo convierte en la data URL que
 * CreateAthlete/UpdateAthlete esperan en `photoUrl`. Rechaza (la promesa se
 * rechaza, no se resuelve con null) un formato o un peso que el backend no
 * va a aceptar, con el mismo mensaje que mostraria un 400 de ahi.
 */
export function readInlinePhoto(file) {
  return new Promise((resolve, reject) => {
    if (!SUPPORTED_TYPES.includes(file.type)) {
      reject(new Error(INLINE_PHOTO_REQUIREMENT));
      return;
    }

    const reader = new FileReader();
    reader.onerror = () => reject(reader.error || new Error('No se pudo leer el archivo.'));
    reader.onload = () => {
      const dataUrl = reader.result;
      if (typeof dataUrl !== 'string' || !dataUrl.startsWith(PREFIX) || dataUrl.length > MAX_LENGTH) {
        reject(new Error(INLINE_PHOTO_REQUIREMENT));
        return;
      }
      resolve(dataUrl);
    };
    reader.readAsDataURL(file);
  });
}

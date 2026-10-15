// Espejo del lado del cliente de SportFrog.Api.Infrastructure.Validation.
// InlinePhoto en el backend: mismos tres formatos, mismo limite de tamano,
// medido igual (sobre el texto de la data URL, que pesa un tercio mas que
// los bytes que lleva adentro -- por eso el limite de 5 MB de texto ronda
// una imagen de 4 MB). Repetido aca a proposito: el backend sigue siendo
// quien decide si el archivo se guarda, esto solo evita un viaje de red
// para avisar de un archivo que el backend va a rechazar igual.
const PHOTO_TYPES = ['image/png', 'image/jpeg', 'image/webp'];
const PHOTO_PREFIXES = ['data:image/'];
const PHOTO_MAX_LENGTH = 5 * 1024 * 1024;

export const INLINE_PHOTO_REQUIREMENT =
  `La foto tiene que ser PNG, JPEG o WebP, de menos de ${PHOTO_MAX_LENGTH / (1024 * 1024)} MB.`;

/**
 * Lee un archivo con FileReader y lo convierte en una data URL, rechazando
 * (la promesa se rechaza, no se resuelve con null) lo que no cumpla el tipo
 * declarado, el prefijo de lo leído o el peso dados. Compartido entre
 * readInlinePhoto y readInlineReceipt -- los dos son la misma mecanica de
 * lectura, solo cambia que aceptan.
 */
function readDataUrl(file, { types, prefixes, maxLength, requirement }) {
  return new Promise((resolve, reject) => {
    if (!types.includes(file.type)) {
      reject(new Error(requirement));
      return;
    }

    const reader = new FileReader();
    reader.onerror = () => reject(reader.error || new Error('No se pudo leer el archivo.'));
    reader.onload = () => {
      const dataUrl = reader.result;
      if (typeof dataUrl !== 'string'
        || !prefixes.some((prefix) => dataUrl.startsWith(prefix))
        || dataUrl.length > maxLength) {
        reject(new Error(requirement));
        return;
      }
      resolve(dataUrl);
    };
    reader.readAsDataURL(file);
  });
}

/**
 * Lee un archivo elegido por la persona y lo convierte en la data URL que
 * CreateAthlete/UpdateAthlete esperan en `photoUrl`.
 */
export function readInlinePhoto(file) {
  return readDataUrl(file, {
    types: PHOTO_TYPES,
    prefixes: PHOTO_PREFIXES,
    maxLength: PHOTO_MAX_LENGTH,
    requirement: INLINE_PHOTO_REQUIREMENT,
  });
}

// Espejo de SportFrog.Api.Infrastructure.Validation.InlineReceipt: los
// mismos tres formatos de foto, mas PDF -- un comprobante es a veces el
// export de una app de banco, no una foto. El limite es mas generoso que
// el de una foto por lo mismo que ImageNormalizer usa un lado largo mayor
// para un comprobante: hay un numero de transaccion que tiene que seguir
// siendo legible.
const RECEIPT_TYPES = [...PHOTO_TYPES, 'application/pdf'];
const RECEIPT_PREFIXES = [...PHOTO_PREFIXES, 'data:application/pdf'];
const RECEIPT_MAX_LENGTH = 8 * 1024 * 1024;

export const INLINE_RECEIPT_REQUIREMENT =
  `El comprobante tiene que ser una foto (PNG, JPEG o WebP) o un PDF, de menos de ` +
  `${RECEIPT_MAX_LENGTH / (1024 * 1024)} MB.`;

/**
 * Lo mismo que readInlinePhoto, para el comprobante de un pago: acepta
 * tambien un PDF.
 */
export function readInlineReceipt(file) {
  return readDataUrl(file, {
    types: RECEIPT_TYPES,
    prefixes: RECEIPT_PREFIXES,
    maxLength: RECEIPT_MAX_LENGTH,
    requirement: INLINE_RECEIPT_REQUIREMENT,
  });
}

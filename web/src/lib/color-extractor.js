/**
 * Extrae una paleta de colores dominantes y armónicos desde una imagen (dataUrl o URL).
 * Utiliza un canvas en memoria para analizar los píxeles sin librerías externas.
 */

function rgbToHex(r, g, b) {
  const toHex = (c) => Math.min(255, Math.max(0, c)).toString(16).padStart(2, '0');
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
}

function colorDistance(c1, c2) {
  return Math.sqrt(
    Math.pow(c1.r - c2.r, 2) +
    Math.pow(c1.g - c2.g, 2) +
    Math.pow(c1.b - c2.b, 2)
  );
}

export async function extractColorsFromImage(imageUrl, maxColors = 6) {
  return new Promise((resolve) => {
    if (!imageUrl) {
      resolve([]);
      return;
    }

    const img = new Image();
    img.crossOrigin = 'Anonymous';

    img.onload = () => {
      try {
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        const size = 120;
        canvas.width = size;
        canvas.height = size;

        ctx.drawImage(img, 0, 0, size, size);
        const imageData = ctx.getImageData(0, 0, size, size).data;

        const colorCounts = {};

        for (let i = 0; i < imageData.length; i += 16) {
          const r = imageData[i];
          const g = imageData[i + 1];
          const b = imageData[i + 2];
          const a = imageData[i + 3];

          if (a < 128) continue; // Ignorar alfa bajo (transparencias)

          // Ignorar blancos puros y negros profundos para paletas vivas
          const brightness = (r * 299 + g * 587 + b * 114) / 1000;
          if (brightness > 245 || brightness < 15) continue;

          // Cuantizar en pasos de 24 para agrupar tonos similares
          const qR = Math.round(r / 24) * 24;
          const qG = Math.round(g / 24) * 24;
          const qB = Math.round(b / 24) * 24;

          const key = `${qR},${qG},${qB}`;
          colorCounts[key] = (colorCounts[key] || 0) + 1;
        }

        // Ordenar por frecuencia de aparición
        const sortedColors = Object.keys(colorCounts)
          .map((key) => {
            const [r, g, b] = key.split(',').map(Number);
            return { r, g, b, count: colorCounts[key] };
          })
          .sort((a, b) => b.count - a.count);

        // Filtrar colores muy similares entre sí (mínima distancia euclidiana 45)
        const distinctColors = [];
        for (const col of sortedColors) {
          const isTooSimilar = distinctColors.some(
            (existing) => colorDistance(col, existing) < 45
          );
          if (!isTooSimilar) {
            distinctColors.push(col);
          }
          if (distinctColors.length >= maxColors) break;
        }

        const hexList = distinctColors.map((c) => rgbToHex(c.r, c.g, c.b));
        resolve(hexList);
      } catch (err) {
        console.error('Error al extraer colores de la imagen:', err);
        resolve([]);
      }
    };

    img.onerror = () => {
      resolve([]);
    };

    img.src = imageUrl;
  });
}

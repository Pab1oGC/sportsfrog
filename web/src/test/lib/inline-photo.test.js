import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { readInlinePhoto, INLINE_PHOTO_REQUIREMENT } from "src/lib/inline-photo";

// MAX_LENGTH no se exporta (es un detalle interno del módulo) -- se replica
// acá solo para poder construir los casos límite. Si el módulo cambia este
// número, esta prueba deja de reflejar el límite real y hay que actualizarla
// a mano; eso es preferible a exportar una constante que nadie más necesita.
const MAX_LENGTH = 5 * 1024 * 1024;

/**
 * Reemplaza el FileReader real por uno controlado a mano: la validación que
 * prueba este archivo es toda sobre qué hace readInlinePhoto con lo que
 * *devuelve* la lectura (tipo, prefijo, largo), no sobre si jsdom sabe
 * codificar un archivo real a base64. Simularlo evita además construir
 * archivos de varios MB de verdad en cada corrida de la suite.
 */
class FileReaderFalso {
  readAsDataURL(file) {
    queueMicrotask(() => {
      if (file.__error !== undefined) {
        this.error = file.__error;
        this.onerror?.();
      } else {
        this.result = file.__result;
        this.onload?.();
      }
    });
  }
}

function archivo({ type = "image/png", result = "data:image/png;base64,AAAA", error } = {}) {
  return { type, __result: result, __error: error };
}

let FileReaderReal;

beforeEach(() => {
  FileReaderReal = globalThis.FileReader;
  globalThis.FileReader = FileReaderFalso;
});

afterEach(() => {
  globalThis.FileReader = FileReaderReal;
});

describe("readInlinePhoto", () => {
  it("rechaza un tipo no soportado sin llegar a leer el archivo", async () => {
    await expect(readInlinePhoto(archivo({ type: "image/gif" }))).rejects.toThrow(INLINE_PHOTO_REQUIREMENT);
  });

  it("rechaza un archivo que no es imagen en absoluto", async () => {
    await expect(readInlinePhoto(archivo({ type: "application/pdf" }))).rejects.toThrow(INLINE_PHOTO_REQUIREMENT);
  });

  it.each(["image/png", "image/jpeg", "image/webp"])("acepta %s y resuelve con la data URL leída", async (type) => {
    const dataUrl = `data:${type};base64,QUJD`;
    await expect(readInlinePhoto(archivo({ type, result: dataUrl }))).resolves.toBe(dataUrl);
  });

  it("rechaza si lo leído no es una data URL de imagen (prefijo equivocado)", async () => {
    await expect(
      readInlinePhoto(archivo({ result: "data:text/plain;base64,AAAA" })),
    ).rejects.toThrow(INLINE_PHOTO_REQUIREMENT);
  });

  it("rechaza si el resultado de la lectura no es una cadena", async () => {
    await expect(readInlinePhoto(archivo({ result: null }))).rejects.toThrow(INLINE_PHOTO_REQUIREMENT);
  });

  it("acepta una data URL de exactamente el límite de tamaño", async () => {
    const dataUrl = "data:image/png;base64," + "A".repeat(MAX_LENGTH - "data:image/png;base64,".length);
    expect(dataUrl.length).toBe(MAX_LENGTH);
    await expect(readInlinePhoto(archivo({ result: dataUrl }))).resolves.toBe(dataUrl);
  });

  it("rechaza una data URL que pasa el límite de tamaño por un solo caracter", async () => {
    const dataUrl = "data:image/png;base64," + "A".repeat(MAX_LENGTH - "data:image/png;base64,".length + 1);
    expect(dataUrl.length).toBe(MAX_LENGTH + 1);
    await expect(readInlinePhoto(archivo({ result: dataUrl }))).rejects.toThrow(INLINE_PHOTO_REQUIREMENT);
  });

  it("el mensaje de rechazo por formato/tamaño es el mismo mensaje que mostraría un 400 del backend", () => {
    expect(INLINE_PHOTO_REQUIREMENT).toContain("PNG");
    expect(INLINE_PHOTO_REQUIREMENT).toContain("5 MB");
  });

  it("un error de lectura (reader.error presente) rechaza con ese error, no con uno genérico", async () => {
    const error = new Error("boom del disco");
    await expect(readInlinePhoto(archivo({ error }))).rejects.toBe(error);
  });

  it("un error de lectura sin reader.error rechaza con un mensaje genérico legible", async () => {
    await expect(readInlinePhoto(archivo({ error: null }))).rejects.toThrow("No se pudo leer el archivo.");
  });
});

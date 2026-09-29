import { beforeEach, describe, expect, it } from "vitest";
import { guardarRenovacion, leerRenovacion, olvidarRenovacion } from "src/auth/context/jwt/session-store";
// Mismo archivo que arriba, pero importado por ruta relativa en vez de por
// el alias "src/..." -- exactamente como lo hacen, en producción,
// lib/axios.js (alias) y auth/context/jwt/action.js (relativo) entre sí.
import * as viaRelative from "../auth/context/jwt/session-store";

describe("alias 'src' vs import relativo (guarda de vite.config.js)", () => {
  beforeEach(() => {
    olvidarRenovacion();
  });

  it("ambas rutas de import resuelven al mismo módulo, no a dos copias con estado propio", () => {
    // Esta prueba existe para proteger vite.config.js:10-24 (ver el
    // comentario ahí): con separadores nativos de Windows sin normalizar,
    // Vite le daba a este archivo dos identificadores de módulo distintos
    // según se lo importara por alias o por ruta relativa -- y por lo tanto
    // dos variables `enMemoria` separadas. Un refresh token guardado por una
    // vía (con "recordarme" apagado, o sea sin tocar localStorage) se veía
    // "ausente" leído por la otra. Si vitest.config.js alguna vez deja de
    // reutilizar ese alias, esta prueba es la que lo nota.
    guardarRenovacion("token-de-prueba", /* recordar */ false);

    expect(viaRelative.leerRenovacion()).toBe("token-de-prueba");
    expect(viaRelative.leerRenovacion()).toBe(leerRenovacion());
  });
});

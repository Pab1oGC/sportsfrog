import { describe, expect, it, beforeEach } from "vitest";
import { jwtDecode, isValidToken, setSession } from "src/auth/context/jwt/utils";
import { JWT_STORAGE_KEY } from "src/auth/context/jwt/constant";

/** Codifica un objeto como el segmento base64url de un JWT (sin firmar de verdad). */
function base64UrlEncode(obj) {
  const json = JSON.stringify(obj);
  const base64 = btoa(unescape(encodeURIComponent(json)));
  return base64.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

/** Arma un token de N segmentos (3 por defecto) para simular distintas formas de token roto. */
function construirToken(payload, { segmentos = 3 } = {}) {
  const header = base64UrlEncode({ alg: "HS256", typ: "JWT" });
  const partes = [header, base64UrlEncode(payload), "firma-falsa"];
  return partes.slice(0, segmentos).join(".");
}

describe("jwtDecode", () => {
  it("decodifica el payload de un token bien formado", () => {
    expect(jwtDecode(construirToken({ sub: "user-1", exp: 999999999999 }))).toEqual({
      sub: "user-1",
      exp: 999999999999,
    });
  });

  it("decodifica correctamente caracteres no ASCII en el payload (acentos, ñ)", () => {
    expect(jwtDecode(construirToken({ nombre: "José Ñáñez" }))).toEqual({ nombre: "José Ñáñez" });
  });

  it("null, undefined o cadena vacía dan null", () => {
    expect(jwtDecode(null)).toBeNull();
    expect(jwtDecode(undefined)).toBeNull();
    expect(jwtDecode("")).toBeNull();
  });

  it("un token sin al menos dos segmentos (sin punto) da null", () => {
    expect(jwtDecode("no-es-un-jwt")).toBeNull();
  });

  it("un payload base64 corrupto hace que la función lance, no que devuelva null", () => {
    // jwtDecode no atrapa sus propios errores -- es isValidToken quien lo
    // envuelve en try/catch. Llamado directo con basura, revienta.
    expect(() => jwtDecode("header.!!!no-es-base64!!!.firma")).toThrow();
  });
});

describe("isValidToken", () => {
  it("true con un token no vencido", () => {
    const exp = Date.now() / 1000 + 3600; // vence en una hora
    expect(isValidToken(construirToken({ exp }))).toBe(true);
  });

  it("false con un token vencido", () => {
    const exp = Date.now() / 1000 - 3600; // venció hace una hora
    expect(isValidToken(construirToken({ exp }))).toBe(false);
  });

  it("sin margen de desfase de reloj: un token que vence en el instante exacto ya no es válido", () => {
    const exp = Date.now() / 1000; // exactamente ahora
    expect(isValidToken(construirToken({ exp }))).toBe(false);
  });

  it("false con null, undefined o cadena vacía", () => {
    expect(isValidToken(null)).toBe(false);
    expect(isValidToken(undefined)).toBe(false);
    expect(isValidToken("")).toBe(false);
  });

  it("un token sin campo 'exp' es inválido", () => {
    expect(isValidToken(construirToken({ sub: "user-1" }))).toBeFalsy();
  });

  it("un token corrupto no revienta: el try/catch lo atrapa y devuelve false", () => {
    expect(isValidToken("header.!!!no-es-base64!!!.firma")).toBe(false);
  });

  it("DETALLE DE TIPO: un token de un solo segmento da null, no false (jwtDecode devuelve null y `d && ...` lo propaga)", () => {
    // isValidToken se usa siempre en un `if`, así que null y false se
    // comportan igual ahí -- pero el valor exacto no es un booleano, y algo
    // que comparara con `=== false` se llevaría una sorpresa.
    expect(isValidToken("sin-puntos")).toBeNull();
  });
});

describe("setSession", () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it("con un token, lo guarda en sessionStorage bajo la clave de JWT", () => {
    setSession("un-token-cualquiera");
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBe("un-token-cualquiera");
  });

  it("con null, borra la clave de sessionStorage", () => {
    sessionStorage.setItem(JWT_STORAGE_KEY, "token-viejo");
    setSession(null);
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBeNull();
  });

  it("con undefined o cadena vacía, también borra la clave", () => {
    sessionStorage.setItem(JWT_STORAGE_KEY, "token-viejo");
    setSession(undefined);
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBeNull();

    sessionStorage.setItem(JWT_STORAGE_KEY, "token-viejo");
    setSession("");
    expect(sessionStorage.getItem(JWT_STORAGE_KEY)).toBeNull();
  });
});

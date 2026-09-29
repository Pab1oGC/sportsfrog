import { describe, expect, it } from "vitest";
import { parseLatLng, esEnlaceCorto, linkFromLatLng, embedSrc } from "./google-maps-url";

describe("parseLatLng", () => {
  it("null, undefined o cadena vacía dan null", () => {
    expect(parseLatLng(null)).toBeNull();
    expect(parseLatLng(undefined)).toBeNull();
    expect(parseLatLng("")).toBeNull();
  });

  it("lee el patrón '!3d<lat>!4d<lng>' de un enlace de lugar (el más confiable de los cuatro)", () => {
    const url = "https://www.google.com/maps/place/Foo/data=!4m5!3m4!1s0x0!8m2!3d-17.393!4d-66.157";
    expect(parseLatLng(url)).toEqual({ lat: -17.393, lng: -66.157 });
  });

  it("lee 'q=lat,lng', lo que arma el selector de mapa de una sede", () => {
    expect(parseLatLng("https://maps.google.com/?q=-17.39,-66.15")).toEqual({ lat: -17.39, lng: -66.15 });
  });

  it("lee '@lat,lng', el mapa centrado ahí tras moverlo o hacer zoom", () => {
    expect(parseLatLng("https://www.google.com/maps/@-17.39,-66.15,15z")).toEqual({ lat: -17.39, lng: -66.15 });
  });

  it("lee 'll=lat,lng'", () => {
    expect(parseLatLng("https://maps.google.com/maps?ll=-17.39,-66.15&z=15")).toEqual({ lat: -17.39, lng: -66.15 });
  });

  it("prioriza '!3d!4d' sobre '@lat,lng' cuando el enlace trae los dos (un enlace de lugar real los trae ambos)", () => {
    const url =
      "https://www.google.com/maps/place/Foo/@-17.400,-66.100,15z/data=!4m5!3m4!1s0x0!8m2!3d-17.393!4d-66.157";
    // El pin exacto (!3d/!4d), no el centro del mapa (@), que puede no coincidir
    // si alguien movió el mapa antes de copiar el enlace.
    expect(parseLatLng(url)).toEqual({ lat: -17.393, lng: -66.157 });
  });

  it("decodifica una coma codificada (%2C) antes de buscar", () => {
    expect(parseLatLng("https://maps.google.com/?q=-17.39%2C-66.15")).toEqual({ lat: -17.39, lng: -66.15 });
  });

  it("un enlace que falla al decodificar (escape inválido) sigue intentando con el texto original", () => {
    // "%zz" no es un escape válido -- decodeURIComponent tira URIError, y el
    // catch debe seguir buscando sobre la URL tal cual llegó.
    expect(parseLatLng("https://maps.google.com/?q=-17.39,-66.15&broken=%zz")).toEqual({ lat: -17.39, lng: -66.15 });
  });

  it("un lugar por nombre, sin coordenadas visibles, da null", () => {
    expect(parseLatLng("https://maps.google.com/?q=Estadio+Nacional")).toBeNull();
  });

  it("un enlace corto sin resolver da null (no hay coordenadas que leer todavía)", () => {
    expect(parseLatLng("https://maps.app.goo.gl/abc123")).toBeNull();
  });

  it("soporta coordenadas negativas y positivas en ambos ejes", () => {
    expect(parseLatLng("https://maps.google.com/?q=17.39,66.15")).toEqual({ lat: 17.39, lng: 66.15 });
  });
});

describe("esEnlaceCorto", () => {
  it("true para maps.app.goo.gl", () => {
    expect(esEnlaceCorto("https://maps.app.goo.gl/abc123")).toBe(true);
  });

  it("true para goo.gl", () => {
    expect(esEnlaceCorto("https://goo.gl/xyz")).toBe(true);
  });

  it("false para un enlace normal de Google Maps", () => {
    expect(esEnlaceCorto("https://www.google.com/maps?q=-17.39,-66.15")).toBe(false);
  });

  it("false (no revienta) con una URL malformada", () => {
    expect(esEnlaceCorto("no es una url")).toBe(false);
  });

  it("false con cadena vacía", () => {
    expect(esEnlaceCorto("")).toBe(false);
  });
});

describe("linkFromLatLng", () => {
  it("arma un enlace de consulta con seis decimales, aunque el número tenga menos", () => {
    expect(linkFromLatLng(-17.393, -66.157)).toBe("https://www.google.com/maps?q=-17.393000,-66.157000");
  });

  it("redondea a seis decimales cuando el número tiene más precisión", () => {
    expect(linkFromLatLng(-17.3934567, -66.1)).toBe("https://www.google.com/maps?q=-17.393457,-66.100000");
  });
});

describe("embedSrc", () => {
  it("null o undefined dan null", () => {
    expect(embedSrc(null)).toBeNull();
    expect(embedSrc(undefined)).toBeNull();
  });

  it("un enlace que ya es de /maps/embed se devuelve intacto", () => {
    const embed = "https://www.google.com/maps/embed?pb=abc123";
    expect(embedSrc(embed)).toBe(embed);
  });

  it("con coordenadas legibles, arma un embed de consulta limpio con output=embed", () => {
    expect(embedSrc("https://maps.google.com/?q=-17.39,-66.15")).toBe(
      "https://www.google.com/maps?q=-17.39,-66.15&output=embed",
    );
  });

  it("sin coordenadas legibles (enlace corto sin resolver, o lugar por nombre), da null y no una adivinanza", () => {
    // A propósito null y no un `?q=<url>&output=embed`: eso es justo lo que
    // produce el error "no se pudo mostrar el contenido personalizado" de Google.
    expect(embedSrc("https://maps.app.goo.gl/abc123")).toBeNull();
    expect(embedSrc("https://maps.google.com/?q=Estadio+Nacional")).toBeNull();
  });
});

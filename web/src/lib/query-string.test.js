import { describe, expect, it } from "vitest";
import { withQueryParams } from "./query-string";

describe("withQueryParams", () => {
  it("agrega un único parámetro con '?'", () => {
    expect(withQueryParams("/api/athletes", { search: "juan" })).toBe("/api/athletes?search=juan");
  });

  it("varios parámetros se unen con '&'", () => {
    expect(withQueryParams("/api/athletes", { skip: 0, take: 10 })).toBe("/api/athletes?skip=0&take=10");
  });

  it("descarta null, undefined y cadena vacía, sin que el llamador tenga que limpiarlos", () => {
    expect(withQueryParams("/api/athletes", { search: null, clubId: undefined, name: "", take: 10 })).toBe(
      "/api/athletes?take=10",
    );
  });

  it("conserva 0 y false -- no son 'ausentes', son valores reales", () => {
    expect(withQueryParams("/api/x", { skip: 0, active: false })).toBe("/api/x?skip=0&active=false");
  });

  it("sin parámetros (objeto vacío) devuelve la url intacta, sin '?'", () => {
    expect(withQueryParams("/api/athletes", {})).toBe("/api/athletes");
  });

  it("params undefined (no se pasó el segundo argumento) devuelve la url intacta", () => {
    expect(withQueryParams("/api/athletes", undefined)).toBe("/api/athletes");
  });

  it("todos los valores descartables devuelven la url intacta, no un '?' vacío", () => {
    expect(withQueryParams("/api/athletes", { search: null, clubId: "" })).toBe("/api/athletes");
  });

  it("si la url ya trae un '?' (armada a mano en la propia página), agrega con '&' en vez de duplicarlo", () => {
    expect(withQueryParams("/api/athletes?search=juan", { skip: 0, take: 10 })).toBe(
      "/api/athletes?search=juan&skip=0&take=10",
    );
  });

  it("codifica caracteres especiales en los valores", () => {
    expect(withQueryParams("/api/athletes", { search: "juan pérez" })).toBe("/api/athletes?search=juan+p%C3%A9rez");
  });

  it("convierte números y booleanos a texto igual que como se ven en la URL final", () => {
    expect(withQueryParams("/api/x", { take: 25 })).toContain("take=25");
  });
});

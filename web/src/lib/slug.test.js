import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { SLUG_MIN, SLUG_MAX, aSlug, normalizarSlug, problemaDeSlug, slugDeOrganizacion } from "./slug";

describe("aSlug", () => {
  it("baja a minúsculas", () => {
    expect(aSlug("Copa Apertura")).toBe("copa-apertura");
  });

  it("descompone acentos y les quita la tilde en vez de reemplazarlos por guion", () => {
    // "Relámpago" tiene que dar "relampago", no "rel-mpago".
    expect(aSlug("Relámpago")).toBe("relampago");
  });

  it("la ñ cae por el mismo camino de descomposición y termina en n", () => {
    expect(aSlug("Ñandú")).toBe("nandu");
  });

  it("colapsa cualquier corrida de caracteres no alfanuméricos en un solo guion", () => {
    expect(aSlug("Copa   Apertura!!  2026")).toBe("copa-apertura-2026");
  });

  it("recorta guiones al principio y al final", () => {
    expect(aSlug("  Copa Apertura  ")).toBe("copa-apertura");
    expect(aSlug("---Copa---")).toBe("copa");
  });

  it("conserva números", () => {
    expect(aSlug("Copa 2026")).toBe("copa-2026");
  });

  it("null, undefined o cadena vacía dan cadena vacía", () => {
    expect(aSlug(null)).toBe("");
    expect(aSlug(undefined)).toBe("");
    expect(aSlug("")).toBe("");
  });

  it("recorta a SLUG_MAX caracteres y no deja un guion colgando en el corte", () => {
    // Un nombre larguísimo cuyo corte a los 63 caracteres caería justo sobre
    // un separador: el guion resultante al final se recorta también.
    const largo = "palabra ".repeat(20); // genera muchos separadores
    const resultado = aSlug(largo);
    expect(resultado.length).toBeLessThanOrEqual(SLUG_MAX);
    expect(resultado.endsWith("-")).toBe(false);
  });
});

describe("normalizarSlug", () => {
  it("recorta espacios y baja a minúsculas -- lo mismo que hace la API antes de validar", () => {
    expect(normalizarSlug("  Copa-2026  ")).toBe("copa-2026");
  });

  it("null o undefined dan cadena vacía", () => {
    expect(normalizarSlug(null)).toBe("");
    expect(normalizarSlug(undefined)).toBe("");
  });

  it("no toca guiones ni números, solo mayúsculas y espacios", () => {
    expect(normalizarSlug("Copa--2026")).toBe("copa--2026");
  });
});

describe("problemaDeSlug", () => {
  it("null para un slug válido", () => {
    expect(problemaDeSlug("copa-apertura-2026")).toBeNull();
  });

  it("vacío o solo espacios: 'obligatoria'", () => {
    expect(problemaDeSlug("")).toBe("La direccion es obligatoria.");
    expect(problemaDeSlug("   ")).toBe("La direccion es obligatoria.");
  });

  it(`más corto que SLUG_MIN (${SLUG_MIN}) después de normalizar`, () => {
    expect(problemaDeSlug("ab")).toContain(String(SLUG_MIN));
  });

  it(`más largo que SLUG_MAX (${SLUG_MAX})`, () => {
    expect(problemaDeSlug("a".repeat(SLUG_MAX + 1))).toContain(String(SLUG_MAX));
  });

  it("mayúsculas solas no son un problema: normalizarSlug ya las baja antes de validar la forma", () => {
    expect(problemaDeSlug("Copa-2026")).toBeNull();
  });

  it("un espacio en medio es un problema de forma (no lo arregla la normalización)", () => {
    expect(problemaDeSlug("copa apertura")).not.toBeNull();
  });

  it("un acento o la ñ sin pasar por aSlug es un problema de forma", () => {
    expect(problemaDeSlug("relámpago")).not.toBeNull();
    expect(problemaDeSlug("ñandu")).not.toBeNull();
  });

  it("guion al principio o al final es inválido", () => {
    expect(problemaDeSlug("-copa")).not.toBeNull();
    expect(problemaDeSlug("copa-")).not.toBeNull();
  });

  it("guiones dobles seguidos son inválidos", () => {
    expect(problemaDeSlug("copa--2026")).not.toBeNull();
  });

  it("el mensaje de forma inválida menciona la restricción, no un 'inválido' genérico", () => {
    expect(problemaDeSlug("Copa Apertura")).toMatch(/minusculas|guion/);
  });
});

describe("slugDeOrganizacion", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it("sin organizaciones guardadas, cae en 'tu-liga'", () => {
    expect(slugDeOrganizacion()).toBe("tu-liga");
  });

  it("devuelve el slug de la primera organización guardada", () => {
    localStorage.setItem("organizations", JSON.stringify([{ id: 1, slug: "liga-del-sur" }, { id: 2, slug: "otra" }]));
    expect(slugDeOrganizacion()).toBe("liga-del-sur");
  });

  it("arreglo vacío de organizaciones cae en 'tu-liga'", () => {
    localStorage.setItem("organizations", JSON.stringify([]));
    expect(slugDeOrganizacion()).toBe("tu-liga");
  });

  it("una organización sin campo slug cae en 'tu-liga'", () => {
    localStorage.setItem("organizations", JSON.stringify([{ id: 1 }]));
    expect(slugDeOrganizacion()).toBe("tu-liga");
  });

  it("JSON corrupto en localStorage no revienta: cae en 'tu-liga'", () => {
    localStorage.setItem("organizations", "{not valid json");
    expect(slugDeOrganizacion()).toBe("tu-liga");
  });
});

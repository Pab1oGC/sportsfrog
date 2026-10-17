import { describe, expect, it } from "vitest";
import { motivoPublicacionInhabilitada } from "src/pages/competitions/competition-lifecycle";

function competencia(overrides = {}) {
  return { status: "draft", isPublic: false, ...overrides };
}

describe("motivoPublicacionInhabilitada", () => {
  it("da un motivo para una competencia en borrador todavía no pública", () => {
    expect(motivoPublicacionInhabilitada(competencia())).toBeTruthy();
  });

  it("no da motivo una vez programada", () => {
    expect(motivoPublicacionInhabilitada(competencia({ status: "scheduled" }))).toBeNull();
  });

  it("no da motivo una vez en curso o finalizada", () => {
    expect(motivoPublicacionInhabilitada(competencia({ status: "in_progress" }))).toBeNull();
    expect(motivoPublicacionInhabilitada(competencia({ status: "finished" }))).toBeNull();
  });

  // Un borrador que ya está público no debería poder existir del lado del
  // servidor (ChangeCompetitionStatus fuerza isPublic=false al volver a
  // Draft) -- pero si por lo que sea llegara así, despublicar nunca se
  // rechaza, así que esto no debe bloquear esa dirección.
  it("no da motivo si de alguna forma ya está pública, aunque el estado sea borrador", () => {
    expect(motivoPublicacionInhabilitada(competencia({ isPublic: true }))).toBeNull();
  });

  it("sin competencia, no da motivo", () => {
    expect(motivoPublicacionInhabilitada(null)).toBeNull();
  });
});

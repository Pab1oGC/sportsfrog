import { describe, expect, it } from "vitest";
import { buildCompetitionUpdatePayload } from "src/pages/competitions/portal-studio-save";
import { EMPTY_PORTAL_FORM } from "src/pages/competitions/portal-payload";

function rowBase(overrides = {}) {
  return {
    rulesetId: "r1",
    name: "Copa Apertura",
    slug: "copa-apertura",
    season: "2026",
    format: "league",
    captureLevel: "basic",
    startsOn: "2026-03-01",
    endsOn: "2026-06-01",
    settings: { schedule: { anchorDay: "saturday" }, bulletin: { footer: "Liga Oficial" } },
    ...overrides,
  };
}

describe("buildCompetitionUpdatePayload -- GUARDA DE REGRESIÓN", () => {
  // El PUT es la competencia ENTERA: no hay PATCH parcial. Estas pruebas
  // existen para que, si alguien alguna vez reescribe esta función sin darse
  // cuenta de que hace falta reenviar schedule/bulletin, el cambio rompa acá
  // -- y no en silencio, en el próximo guardado de portal de alguien.

  it("reenvía schedule y bulletin de la competencia tal cual, sin que el formulario del portal los toque", () => {
    const row = rowBase();
    const payload = buildCompetitionUpdatePayload(row, EMPTY_PORTAL_FORM);
    expect(payload.settings.schedule).toEqual({ anchorDay: "saturday" });
    expect(payload.settings.bulletin).toEqual({ footer: "Liga Oficial" });
  });

  it("schedule/bulletin salen de `row`, no de `form` -- cambiar el formulario del portal no los altera", () => {
    const row = rowBase();
    const formConBasuraIrrelevante = { ...EMPTY_PORTAL_FORM, description: "Esto no debería afectar schedule/bulletin" };
    const payload = buildCompetitionUpdatePayload(row, formConBasuraIrrelevante);
    expect(payload.settings.schedule).toEqual(row.settings.schedule);
    expect(payload.settings.bulletin).toEqual(row.settings.bulletin);
  });

  it("sin schedule/bulletin declarados en la competencia, van como null -- nunca undefined (el PUT los borraría igual, pero explícito)", () => {
    const row = rowBase({ settings: {} });
    const payload = buildCompetitionUpdatePayload(row, EMPTY_PORTAL_FORM);
    expect(payload.settings.schedule).toBeNull();
    expect(payload.settings.bulletin).toBeNull();
  });

  it("sin 'settings' en absoluto en la competencia, tampoco revienta", () => {
    const row = rowBase({ settings: undefined });
    expect(() => buildCompetitionUpdatePayload(row, EMPTY_PORTAL_FORM)).not.toThrow();
    const payload = buildCompetitionUpdatePayload(row, EMPTY_PORTAL_FORM);
    expect(payload.settings.schedule).toBeNull();
  });

  it("reenvía los campos ajenos al portal (nombre, slug, reglamento, fechas) tal cual vinieron -- este editor no los toca", () => {
    const row = rowBase();
    const payload = buildCompetitionUpdatePayload(row, EMPTY_PORTAL_FORM);
    expect(payload.rulesetId).toBe(row.rulesetId);
    expect(payload.name).toBe(row.name);
    expect(payload.slug).toBe(row.slug);
    expect(payload.season).toBe(row.season);
    expect(payload.format).toBe(row.format);
    expect(payload.captureLevel).toBe(row.captureLevel);
    expect(payload.startsOn).toBe(row.startsOn);
    expect(payload.endsOn).toBe(row.endsOn);
  });

  it("settings.public sale de buildPortalPayload(form), no de una copia manual", () => {
    const payload = buildCompetitionUpdatePayload(rowBase(), { ...EMPTY_PORTAL_FORM, description: "Torneo anual" });
    expect(payload.settings.public.description).toBe("Torneo anual");
  });
});

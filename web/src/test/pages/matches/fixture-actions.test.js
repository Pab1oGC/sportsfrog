import { describe, expect, it, vi } from "vitest";
import { buildFixtureActions } from "src/pages/matches/fixture-actions";

function partido(overrides = {}) {
  return { id: "m1", status: "scheduled", homeTeamId: "h1", awayTeamId: "a1", ...overrides };
}

function callbacks(overrides = {}) {
  return { setSelMatch: vi.fn(), setError: vi.fn(), setWoOpen: vi.fn(), doStatus: vi.fn(), ...overrides };
}

function etiquetas(m, cb) {
  return buildFixtureActions(m, cb).filter(Boolean).map((a) => a.label);
}

describe("buildFixtureActions -- Walkover", () => {
  it("aparece con un partido programado y los dos equipos definidos", () => {
    expect(etiquetas(partido({ status: "scheduled" }), callbacks())).toContain("Walkover");
  });

  it("no aparece sin homeTeamId ni awayTeamId (llave sorteada sin resolver)", () => {
    expect(etiquetas(partido({ status: "scheduled", homeTeamId: null }), callbacks())).not.toContain("Walkover");
    expect(etiquetas(partido({ status: "scheduled", awayTeamId: null }), callbacks())).not.toContain("Walkover");
  });

  it("no aparece si el partido no está scheduled", () => {
    expect(etiquetas(partido({ status: "in_progress" }), callbacks())).not.toContain("Walkover");
  });

  it("abre el diálogo de walkover con el partido seleccionado", () => {
    const setSelMatch = vi.fn();
    const setWoOpen = vi.fn();
    const [accion] = buildFixtureActions(partido({ status: "scheduled" }), callbacks({ setSelMatch, setWoOpen })).filter(Boolean);
    accion.onClick();
    expect(setSelMatch).toHaveBeenCalledWith(expect.objectContaining({ id: "m1" }));
    expect(setWoOpen).toHaveBeenCalledWith(true);
  });
});

describe("buildFixtureActions -- Reabrir", () => {
  it.each(["cancelled", "walkover", "postponed"])("aparece para un partido en estado '%s'", (status) => {
    expect(etiquetas(partido({ status }), callbacks())).toContain("Reabrir (vuelve a programado)");
  });

  it.each(["scheduled", "in_progress", "finished"])("no aparece para un partido en estado '%s'", (status) => {
    expect(etiquetas(partido({ status }), callbacks())).not.toContain("Reabrir (vuelve a programado)");
  });

  it("dispara doStatus(id, 'scheduled')", () => {
    const doStatus = vi.fn();
    const [accion] = buildFixtureActions(partido({ status: "cancelled" }), callbacks({ doStatus })).filter(Boolean);
    accion.onClick();
    expect(doStatus).toHaveBeenCalledWith("m1", "scheduled");
  });
});

describe("buildFixtureActions -- forma del arreglo", () => {
  it("un partido sin ninguna acción disponible (en curso) da solo huecos 'false'", () => {
    const resultado = buildFixtureActions(partido({ status: "in_progress" }), callbacks()).filter(Boolean);
    expect(resultado).toEqual([]);
  });
});

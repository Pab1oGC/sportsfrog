import { describe, expect, it, vi } from "vitest";
import { buildLiveActions } from "src/pages/matches/live-actions";

const sportConMetricaDeMarcador = { metrics: [{ code: "goal", affectsScore: true }] };
const sportSinMetricaDeMarcador = { metrics: [{ code: "card", affectsScore: false }] };

function partido(overrides = {}) {
  return {
    id: "m1",
    status: "scheduled",
    homeTeamId: "h1",
    awayTeamId: "a1",
    phase: null,
    homeTotal: null,
    awayTotal: null,
    ...overrides,
  };
}

function callbacks(overrides = {}) {
  return {
    sportInfo: undefined,
    setSelMatch: vi.fn(),
    setError: vi.fn(),
    setResOpen: vi.fn(),
    setPoOpen: vi.fn(),
    doStatus: vi.fn(),
    doFinishFromEvents: vi.fn(),
    ...overrides,
  };
}

/** Las acciones reales (sin los `false` que deja el .filter(Boolean) de quien llama), identificadas por label. */
function etiquetas(m, cb) {
  return buildLiveActions(m, cb).filter(Boolean).map((a) => a.label);
}

describe("buildLiveActions -- Iniciar", () => {
  it("aparece con un partido programado y los dos equipos definidos", () => {
    expect(etiquetas(partido({ status: "scheduled" }), callbacks())).toContain("Iniciar");
  });

  it("no aparece sin homeTeamId (llave sorteada sin resolver todavía)", () => {
    expect(etiquetas(partido({ status: "scheduled", homeTeamId: null }), callbacks())).not.toContain("Iniciar");
  });

  it("no aparece sin awayTeamId", () => {
    expect(etiquetas(partido({ status: "scheduled", awayTeamId: null }), callbacks())).not.toContain("Iniciar");
  });

  it("no aparece si el partido no está scheduled", () => {
    expect(etiquetas(partido({ status: "in_progress" }), callbacks())).not.toContain("Iniciar");
  });

  it("dispara doStatus(id, 'in_progress')", () => {
    const doStatus = vi.fn();
    const [accion] = buildLiveActions(partido({ status: "scheduled" }), callbacks({ doStatus })).filter(Boolean);
    accion.onClick();
    expect(doStatus).toHaveBeenCalledWith("m1", "in_progress");
  });
});

describe("buildLiveActions -- Finalizar (con eventos) vs Resultado (manual)", () => {
  it("con sportInfo cargado y una métrica que suma al marcador: ofrece finalizar con los eventos", () => {
    const labels = etiquetas(partido({ status: "in_progress" }), callbacks({ sportInfo: sportConMetricaDeMarcador }));
    expect(labels).toContain("Finalizar con el marcador de los eventos");
    expect(labels).not.toContain("Resultado");
  });

  it("con sportInfo cargado pero sin ninguna métrica que sume (voley): pide el resultado a mano", () => {
    const labels = etiquetas(partido({ status: "in_progress" }), callbacks({ sportInfo: sportSinMetricaDeMarcador }));
    expect(labels).toContain("Resultado");
    expect(labels).not.toContain("Finalizar con el marcador de los eventos");
  });

  it("mientras sportInfo todavía no cargó (undefined), cae en el camino manual -- nunca deja los dos caminos ausentes", () => {
    const labels = etiquetas(partido({ status: "in_progress" }), callbacks({ sportInfo: undefined }));
    expect(labels).toContain("Resultado");
    expect(labels).not.toContain("Finalizar con el marcador de los eventos");
  });

  it("ninguno de los dos aparece si el partido no está in_progress", () => {
    const labels = etiquetas(partido({ status: "scheduled" }), callbacks({ sportInfo: sportConMetricaDeMarcador }));
    expect(labels).not.toContain("Finalizar con el marcador de los eventos");
    expect(labels).not.toContain("Resultado");
  });

  it("'Finalizar con el marcador de los eventos' llama a doFinishFromEvents con el partido", () => {
    const doFinishFromEvents = vi.fn();
    const [accion] = buildLiveActions(partido({ status: "in_progress" }), callbacks({ sportInfo: sportConMetricaDeMarcador, doFinishFromEvents })).filter(Boolean);
    accion.onClick();
    expect(doFinishFromEvents).toHaveBeenCalledWith(expect.objectContaining({ id: "m1" }));
  });

  it("'Resultado' abre el diálogo de resultado con el partido seleccionado", () => {
    const setSelMatch = vi.fn();
    const setResOpen = vi.fn();
    const [accion] = buildLiveActions(partido({ status: "in_progress" }), callbacks({ setSelMatch, setResOpen })).filter(Boolean);
    accion.onClick();
    expect(setSelMatch).toHaveBeenCalledWith(expect.objectContaining({ id: "m1" }));
    expect(setResOpen).toHaveBeenCalledWith(true);
  });
});

describe("buildLiveActions -- Desempate por penales", () => {
  it("aparece solo con el partido finalizado, con fase (eliminatoria) y empatado", () => {
    expect(etiquetas(partido({ status: "finished", phase: "final", homeTotal: 1, awayTotal: 1 }), callbacks())).toContain("Desempate por penales");
  });

  it("no aparece en un empate de fase de grupos (sin fase) -- ahí un empate es un resultado válido", () => {
    expect(etiquetas(partido({ status: "finished", phase: null, homeTotal: 1, awayTotal: 1 }), callbacks())).not.toContain("Desempate por penales");
  });

  it("no aparece si no está empatado", () => {
    expect(etiquetas(partido({ status: "finished", phase: "final", homeTotal: 2, awayTotal: 1 }), callbacks())).not.toContain("Desempate por penales");
  });

  it("no aparece si el partido no está finished", () => {
    expect(etiquetas(partido({ status: "in_progress", phase: "final", homeTotal: 1, awayTotal: 1 }), callbacks())).not.toContain("Desempate por penales");
  });
});

describe("buildLiveActions -- Cancelar", () => {
  it("aparece solo mientras el partido está en curso", () => {
    expect(etiquetas(partido({ status: "in_progress" }), callbacks())).toContain("Cancelar");
    expect(etiquetas(partido({ status: "scheduled" }), callbacks())).not.toContain("Cancelar");
    expect(etiquetas(partido({ status: "finished" }), callbacks())).not.toContain("Cancelar");
  });

  it("dispara doStatus(id, 'cancelled')", () => {
    const doStatus = vi.fn();
    const acciones = buildLiveActions(partido({ status: "in_progress" }), callbacks({ doStatus })).filter(Boolean);
    acciones.find((a) => a.label === "Cancelar").onClick();
    expect(doStatus).toHaveBeenCalledWith("m1", "cancelled");
  });
});

describe("buildLiveActions -- forma del arreglo", () => {
  it("devuelve un arreglo con huecos 'false' para quien llama filtrarlos (no los filtra acá)", () => {
    const resultado = buildLiveActions(partido({ status: "finished", phase: null }), callbacks());
    expect(resultado.some((a) => a === false)).toBe(true);
  });

  it("un partido sin ninguna acción disponible (finalizado, sin fase, sin empate) da solo huecos", () => {
    const resultado = buildLiveActions(partido({ status: "finished", phase: null, homeTotal: 2, awayTotal: 1 }), callbacks()).filter(Boolean);
    expect(resultado).toEqual([]);
  });
});

import { describe, expect, it } from "vitest";
import { resolveMatchOutcome } from "./match-outcome";

function partido(overrides = {}) {
  return {
    status: "scheduled",
    homeTotal: null,
    awayTotal: null,
    liveHomeTotal: null,
    liveAwayTotal: null,
    penaltyHomeScore: null,
    penaltyAwayScore: null,
    ...overrides,
  };
}

describe("resolveMatchOutcome", () => {
  it("sin resultado y sin marcador en vivo: todo apagado, nada que mostrar", () => {
    const r = resolveMatchOutcome(partido({ status: "scheduled" }));
    expect(r).toEqual({
      jugado: false,
      enVivo: false,
      huboPenales: false,
      ganoLocal: false,
      ganoVisita: false,
      homeMostrado: null,
      awayMostrado: null,
    });
  });

  it("en curso con marcador en vivo: muestra el marcador en vivo, no 'vs'", () => {
    const r = resolveMatchOutcome(partido({ status: "in_progress", liveHomeTotal: 1, liveAwayTotal: 0 }));
    expect(r.jugado).toBe(false);
    expect(r.enVivo).toBe(true);
    expect(r.homeMostrado).toBe(1);
    expect(r.awayMostrado).toBe(0);
    // Un partido en curso todavía no tiene ganador: eso solo lo decide el cierre.
    expect(r.ganoLocal).toBe(false);
    expect(r.ganoVisita).toBe(false);
  });

  it("en curso pero sin eventos de marcador todavía: no hay marcador en vivo que mostrar", () => {
    const r = resolveMatchOutcome(partido({ status: "in_progress", liveHomeTotal: null, liveAwayTotal: null }));
    expect(r.enVivo).toBe(false);
    expect(r.homeMostrado).toBeNull();
    expect(r.awayMostrado).toBeNull();
  });

  it("en curso con un solo lado del marcador en vivo cargado: no alcanza, sigue sin marcador", () => {
    const r = resolveMatchOutcome(partido({ status: "in_progress", liveHomeTotal: 2, liveAwayTotal: null }));
    expect(r.enVivo).toBe(false);
  });

  it("terminado, gana el local sin penales", () => {
    const r = resolveMatchOutcome(partido({ status: "finished", homeTotal: 2, awayTotal: 1 }));
    expect(r.jugado).toBe(true);
    expect(r.huboPenales).toBe(false);
    expect(r.ganoLocal).toBe(true);
    expect(r.ganoVisita).toBe(false);
    expect(r.homeMostrado).toBe(2);
    expect(r.awayMostrado).toBe(1);
  });

  it("terminado, gana la visita sin penales", () => {
    const r = resolveMatchOutcome(partido({ status: "finished", homeTotal: 0, awayTotal: 3 }));
    expect(r.ganoLocal).toBe(false);
    expect(r.ganoVisita).toBe(true);
  });

  it("empate en el marcador y sin penales cargados: empate real, ningún lado gana", () => {
    const r = resolveMatchOutcome(partido({ status: "finished", homeTotal: 1, awayTotal: 1 }));
    expect(r.huboPenales).toBe(false);
    expect(r.ganoLocal).toBe(false);
    expect(r.ganoVisita).toBe(false);
  });

  it("empate en el marcador con penales: el ganador lo decide la tanda, no el marcador empatado", () => {
    const r = resolveMatchOutcome(
      partido({ status: "finished", homeTotal: 1, awayTotal: 1, penaltyHomeScore: 4, penaltyAwayScore: 3 }),
    );
    expect(r.huboPenales).toBe(true);
    expect(r.ganoLocal).toBe(true);
    expect(r.ganoVisita).toBe(false);
    // El marcador mostrado sigue siendo el de los 90 minutos, no los penales.
    expect(r.homeMostrado).toBe(1);
    expect(r.awayMostrado).toBe(1);
  });

  it("empate con penales, gana la visita en la tanda", () => {
    const r = resolveMatchOutcome(
      partido({ status: "finished", homeTotal: 2, awayTotal: 2, penaltyHomeScore: 3, penaltyAwayScore: 5 }),
    );
    expect(r.ganoLocal).toBe(false);
    expect(r.ganoVisita).toBe(true);
  });

  it("penaltyHomeScore presente pero sin empate en el marcador: los penales se ignoran, decide el marcador", () => {
    // huboPenales exige homeTotal === awayTotal -- un walkover con datos de
    // penales de una edición anterior del formulario no debería colarse acá.
    const r = resolveMatchOutcome(
      partido({ status: "finished", homeTotal: 3, awayTotal: 1, penaltyHomeScore: 1, penaltyAwayScore: 9 }),
    );
    expect(r.huboPenales).toBe(false);
    expect(r.ganoLocal).toBe(true);
    expect(r.ganoVisita).toBe(false);
  });

  it("un resultado de 0 a 0 sí cuenta como jugado (jugado exige != null, no truthy)", () => {
    const r = resolveMatchOutcome(partido({ status: "finished", homeTotal: 0, awayTotal: 0 }));
    expect(r.jugado).toBe(true);
    expect(r.homeMostrado).toBe(0);
    expect(r.awayMostrado).toBe(0);
  });

  it("jugado manda sobre el estado 'in_progress': si ya hay resultado, no se usa el marcador en vivo", () => {
    const r = resolveMatchOutcome(
      partido({ status: "in_progress", homeTotal: 2, awayTotal: 1, liveHomeTotal: 9, liveAwayTotal: 9 }),
    );
    expect(r.jugado).toBe(true);
    expect(r.enVivo).toBe(false);
    expect(r.homeMostrado).toBe(2);
    expect(r.awayMostrado).toBe(1);
  });
});

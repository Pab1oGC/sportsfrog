import { describe, expect, it } from "vitest";
import { EN_VUELO, hayLoteEnVuelo, intervaloDeListaDeLotes, intervaloDeDetalleDeLote } from "src/pages/documents/batch-polling";

function lote(status) {
  return { status };
}

describe("EN_VUELO", () => {
  it("solo 'queued' y 'running' cuentan como en vuelo -- 'finished' y 'failed' no", () => {
    expect(EN_VUELO.queued).toBe(true);
    expect(EN_VUELO.running).toBe(true);
    expect(EN_VUELO.finished).toBeUndefined();
    expect(EN_VUELO.failed).toBeUndefined();
  });
});

describe("hayLoteEnVuelo", () => {
  it("true si al menos un lote de la lista está en cola o procesando", () => {
    expect(hayLoteEnVuelo([lote("finished"), lote("queued")])).toBe(true);
    expect(hayLoteEnVuelo([lote("running")])).toBe(true);
  });

  it("false si todos los lotes ya terminaron (finished o failed)", () => {
    expect(hayLoteEnVuelo([lote("finished"), lote("failed")])).toBe(false);
  });

  it("false con lista vacía o sin datos todavía (undefined)", () => {
    expect(hayLoteEnVuelo([])).toBe(false);
    expect(hayLoteEnVuelo(undefined)).toBe(false);
  });
});

describe("intervaloDeListaDeLotes -- el sondeo se apaga solo", () => {
  it("2000ms mientras algún lote siga en vuelo", () => {
    expect(intervaloDeListaDeLotes([lote("queued")])).toBe(2000);
    expect(intervaloDeListaDeLotes([lote("finished"), lote("running")])).toBe(2000);
  });

  it("0 (apagado) en cuanto todos terminaron", () => {
    expect(intervaloDeListaDeLotes([lote("finished"), lote("failed")])).toBe(0);
  });

  it("0 sin datos todavía", () => {
    expect(intervaloDeListaDeLotes(undefined)).toBe(0);
  });
});

describe("intervaloDeDetalleDeLote", () => {
  it("1500ms mientras el lote puntual siga en vuelo", () => {
    expect(intervaloDeDetalleDeLote(lote("running"))).toBe(1500);
    expect(intervaloDeDetalleDeLote(lote("queued"))).toBe(1500);
  });

  it("0 una vez que el lote terminó", () => {
    expect(intervaloDeDetalleDeLote(lote("finished"))).toBe(0);
    expect(intervaloDeDetalleDeLote(lote("failed"))).toBe(0);
  });

  it("0 sin lote todavía (null, antes de elegir uno para ver el detalle)", () => {
    expect(intervaloDeDetalleDeLote(null)).toBe(0);
    expect(intervaloDeDetalleDeLote(undefined)).toBe(0);
  });
});

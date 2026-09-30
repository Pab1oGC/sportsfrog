import { describe, expect, it } from "vitest";
import { validarMinuto } from "src/pages/matches/minute-validation";

const sportInfo = { scoreMode: "cumulative", periodMinutes: 45, periodMaxExtraMinutes: 5, periodLabel: "Tiempo" };

describe("validarMinuto", () => {
  it("sin sportInfo todavía, no hay ventana ni ayuda que mostrar", () => {
    const r = validarMinuto(undefined, "1", "20");
    expect(r.ventana).toBeNull();
    expect(r.minutoFuera).toBe(false);
    expect(r.ayudaDeMinuto).toBeUndefined();
  });

  it("sin período elegido todavía (''), tampoco hay ventana", () => {
    const r = validarMinuto(sportInfo, "", "20");
    expect(r.ventana).toBeNull();
    expect(r.ayudaDeMinuto).toBeUndefined();
  });

  it("un deporte por sets o juzgado (sin scoreMode cumulative) no tiene ventana de minuto", () => {
    const r = validarMinuto({ isPlayedInSets: true }, "1", "20");
    expect(r.ventana).toBeNull();
  });

  it("con período elegido pero sin minuto tipeado todavía, muestra el rango como ayuda pasiva, sin marcar error", () => {
    const r = validarMinuto(sportInfo, "1", "");
    expect(r.ventana).toEqual({ desde: 0, regular: 45, hasta: 50, adicional: 5 });
    expect(r.minutoFuera).toBe(false);
    expect(r.ayudaDeMinuto).toBe("1–45 (+5)");
  });

  it("un minuto dentro de lo regular no marca error, y muestra el rango como ayuda", () => {
    const r = validarMinuto(sportInfo, "1", "20");
    expect(r.minutoFuera).toBe(false);
    expect(r.ayudaDeMinuto).toBe("1–45 (+5)");
  });

  it("un minuto dentro del adicional no marca error, y lo aclara como 'Adicional: regular+extra'", () => {
    const r = validarMinuto(sportInfo, "1", "48");
    expect(r.minutoFuera).toBe(false);
    expect(r.ayudaDeMinuto).toBe("Adicional: 45+3");
  });

  it("un minuto más allá del adicional permitido marca error, con el rango válido en el mensaje", () => {
    const r = validarMinuto(sportInfo, "1", "60");
    expect(r.minutoFuera).toBe(true);
    expect(r.ayudaDeMinuto).toBe("Fuera de Tiempo 1: va de 1–45 (+5)");
  });

  it("el período 2 tiene su propia ventana (arranca en 46, no en 0) -- un minuto del período 1 queda fuera de rango", () => {
    const r = validarMinuto(sportInfo, "2", "10");
    expect(r.ventana).toEqual({ desde: 46, regular: 90, hasta: 95, adicional: 5 });
    expect(r.minutoFuera).toBe(true);
    expect(r.ayudaDeMinuto).toBe("Fuera de Tiempo 2: va de 46–90 (+5)");
  });

  it("el minuto exacto en el límite (hasta) no cuenta como fuera de rango", () => {
    const r = validarMinuto(sportInfo, "1", "50");
    expect(r.minutoFuera).toBe(false);
  });
});

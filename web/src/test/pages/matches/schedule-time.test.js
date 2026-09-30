import { describe, expect, it } from "vitest";
import { calcularUtcOffsetMinutes } from "src/pages/matches/schedule-time";

describe("calcularUtcOffsetMinutes", () => {
  it("sin hora de inicio, da null -- no hay nada que anclar todavía", () => {
    expect(calcularUtcOffsetMinutes("2026-06-15", "")).toBeNull();
    expect(calcularUtcOffsetMinutes("2026-06-15", null)).toBeNull();
  });

  it("con fecha y hora, da un número de minutos (el offset local del proceso para ese instante)", () => {
    const offset = calcularUtcOffsetMinutes("2026-06-15", "16:00");
    expect(typeof offset).toBe("number");
    expect(Number.isFinite(offset)).toBe(true);
  });

  it("es el mismo valor que -getTimezoneOffset() para ese día y hora exactos", () => {
    const dia = "2026-06-15";
    const hora = "16:00";
    expect(calcularUtcOffsetMinutes(dia, hora)).toBe(-new Date(`${dia}T${hora}`).getTimezoneOffset());
  });

  it("una combinación de fecha/hora que no forma una fecha real da null, no NaN", () => {
    expect(calcularUtcOffsetMinutes("fecha-invalida", "16:00")).toBeNull();
  });
});

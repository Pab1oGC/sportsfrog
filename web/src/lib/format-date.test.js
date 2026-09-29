import { describe, expect, it } from "vitest";
import { fechaHora, aFechaInput } from "./format-date";

// Estas pruebas dependen de que el corredor tenga la zona horaria fijada
// en America/Argentina/Buenos_Aires (UTC-3, sin horario de verano) -- ver
// vitest.config.js. Cada hora UTC de acá abajo está elegida para que, al
// restarle 3 horas, dé un resultado fácil de verificar a mano.
describe("fechaHora", () => {
  it("cadena vacía, null o undefined dan cadena vacía", () => {
    expect(fechaHora("")).toBe("");
    expect(fechaHora(null)).toBe("");
    expect(fechaHora(undefined)).toBe("");
  });

  it("formato fijo dd/mm/aaaa hh:mm AM/PM, no el de toLocaleString()", () => {
    // 00:00 UTC - 3h = 21:00 del día anterior.
    expect(fechaHora("2026-05-01T00:00:00Z")).toBe("30/04/2026 09:00 PM");
  });

  it("rellena con cero día, mes, hora y minuto de un solo dígito", () => {
    // 12:05 UTC - 3h = 09:05 local.
    expect(fechaHora("2026-03-05T12:05:00Z")).toBe("05/03/2026 09:05 AM");
  });

  it("mediodía (12:xx) se muestra como 12, no como 00, y en PM", () => {
    // 15:30 UTC - 3h = 12:30 local.
    expect(fechaHora("2026-06-15T15:30:00Z")).toBe("15/06/2026 12:30 PM");
  });

  it("medianoche (00:xx) se muestra como 12, no como 00, y en AM", () => {
    // 03:00 UTC - 3h = 00:00 local.
    expect(fechaHora("2026-01-10T03:00:00Z")).toBe("10/01/2026 12:00 AM");
  });

  it("una hora de la tarde distinta de las 12 resta 12 para el reloj de 12 horas", () => {
    // 22:00 UTC - 3h = 19:00 local -> 7 PM.
    expect(fechaHora("2026-07-20T22:00:00Z")).toBe("20/07/2026 07:00 PM");
  });
});

describe("aFechaInput", () => {
  it("cadena vacía, null o undefined dan cadena vacía", () => {
    expect(aFechaInput("")).toBe("");
    expect(aFechaInput(null)).toBe("");
    expect(aFechaInput(undefined)).toBe("");
  });

  it("da hora local sin zona, formato que espera <input type=datetime-local>", () => {
    expect(aFechaInput("2026-05-01T00:00:00Z")).toBe("2026-04-30T21:00");
  });

  it("rellena con cero mes, día, hora y minuto de un solo dígito", () => {
    expect(aFechaInput("2026-03-05T12:05:00Z")).toBe("2026-03-05T09:05");
  });

  it("es consistente con fechaHora para la misma fecha (mismo día y hora locales)", () => {
    const iso = "2026-06-15T15:30:00Z";
    expect(aFechaInput(iso)).toBe("2026-06-15T12:30");
    expect(fechaHora(iso)).toBe("15/06/2026 12:30 PM");
  });
});

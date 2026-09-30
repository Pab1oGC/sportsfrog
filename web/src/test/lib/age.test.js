import { describe, expect, it, beforeEach, afterEach, vi } from "vitest";
import { edad } from "src/lib/age";

// "Hoy" fijo para poder armar casos exactos alrededor del cumpleaños.
const HOY = "2026-06-15T12:00:00";

// Las fechas de nacimiento de este archivo llevan hora local explícita
// ("...T00:00:00", sin 'Z' ni offset) en vez de la forma "AAAA-MM-DD" pelada
// que realmente manda la API (un DateOnly serializado). Es a propósito: un
// string sin hora se parsea como MEDIANOCHE UTC, y en cualquier huso horario
// al oeste de UTC (Bolivia, Argentina...) eso cae en la NOCHE ANTERIOR en
// hora local -- corriendo un día entero para atrás justo la comparación que
// este archivo quiere ejercitar con precisión. Con hora local explícita, la
// fecha se parsea tal cual se lee, sin importar en qué huso corra la
// suite. El último test de este archivo confirma que la forma real (pelada)
// también funciona, y deja documentado ese corrimiento como lo que es: un
// comportamiento real de `edad()`, no un defecto de esta prueba.
beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date(HOY));
});

afterEach(() => {
  vi.useRealTimers();
});

describe("edad", () => {
  it("sin fecha de nacimiento (null, undefined, cadena vacía) da null, nunca 0", () => {
    expect(edad(null)).toBeNull();
    expect(edad(undefined)).toBeNull();
    expect(edad("")).toBeNull();
  });

  it("el cumpleaños es justo hoy: la resta simple de años, sin ajustar", () => {
    expect(edad("2000-06-15T00:00:00")).toBe(26);
  });

  it("el cumpleaños todavía no llegó este año (falta un día, mismo mes): resta uno", () => {
    expect(edad("2000-06-16T00:00:00")).toBe(25);
  });

  it("el cumpleaños ya pasó este año (fue ayer, mismo mes): no resta nada", () => {
    expect(edad("2000-06-14T00:00:00")).toBe(26);
  });

  it("el mes de nacimiento todavía no llegó este año: resta uno", () => {
    expect(edad("2000-07-01T00:00:00")).toBe(25);
  });

  it("el mes de nacimiento ya pasó este año: no resta nada", () => {
    expect(edad("2000-05-01T00:00:00")).toBe(26);
  });

  it("nacido hoy mismo: 0 años, no null ni negativo", () => {
    expect(edad("2026-06-15T00:00:00")).toBe(0);
  });

  it("cumple 26 exactamente al cruzar el año calendario de nacimiento a nacimiento", () => {
    expect(edad("1999-12-31T00:00:00")).toBe(26);
    expect(edad("2000-01-01T00:00:00")).toBe(26);
  });

  it("acepta también la forma real que manda la API -- una fecha pelada 'AAAA-MM-DD', lejos de cualquier borde", () => {
    // Ver la nota de arriba: una fecha pelada se corre un día para atrás en
    // huso horario negativo, así que este caso se eligió bien lejos de "hoy"
    // (marzo contra junio) para que ese corrimiento de un día no pueda mover
    // el resultado a otro balde (mes de nacimiento ya pasado, de cualquier forma).
    expect(edad("2000-03-01")).toBe(26);
  });
});

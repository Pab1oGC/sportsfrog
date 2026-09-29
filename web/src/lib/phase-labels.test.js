import { describe, expect, it } from "vitest";
import { FASES, nombreFase } from "./phase-labels";

describe("FASES", () => {
  it("mapea las claves reales que devuelve Bracket.Phase() en el backend", () => {
    // Las claves viejas (round_of_32, quarterfinal, third_place) no
    // matcheaban ninguna fase real -- estas sí, ver el comentario del
    // archivo. Fijar el mapa completo acá para que un cambio accidental de
    // alguna clave se note.
    expect(FASES).toEqual({
      dieciseisavos: "Dieciseisavos",
      octavos: "Octavos de final",
      cuartos: "Cuartos de final",
      semifinal: "Semifinales",
      final: "Final",
      repechaje: "Repechaje",
      "repechaje bronce": "Repechaje - Bronce",
    });
  });
});

describe("nombreFase", () => {
  it.each(Object.entries(FASES))("usa el nombre en español para la fase conocida '%s'", (codigo, esperado) => {
    expect(nombreFase(codigo)).toBe(esperado);
  });

  it("una fase desconocida (más allá de dieciseisavos, sin nombre fijo) capitaliza la primera letra del código crudo", () => {
    // Bracket.Phase() devuelve "ronda N" para lo que sigue a dieciseisavos --
    // ninguna tabla estática lo va a tener cargado nunca.
    expect(nombreFase("ronda 5")).toBe("Ronda 5");
  });

  it("una cadena vacía no revienta: da cadena vacía (no hay nada que capitalizar)", () => {
    expect(nombreFase("")).toBe("");
  });

  it("DEFECTO CONOCIDO: null o undefined hacen que conMayuscula reviente con TypeError", () => {
    // Documentado en el plan de pruebas como algo a fijar o arreglar -- esta
    // prueba fija el comportamiento actual (revienta) para que quien lo
    // toque sepa que está cambiando algo a propósito, no rompiendo algo que
    // ya andaba. Bracket.Phase() nunca manda null en la práctica, así que
    // hoy no se dispara en producción -- pero un valor inesperado del
    // backend lo dispararía.
    expect(() => nombreFase(null)).toThrow(TypeError);
    expect(() => nombreFase(undefined)).toThrow(TypeError);
  });
});

import { describe, expect, it, vi } from "vitest";
import { bloquearNoEnteros, soloDigitos, bloquearNegativos, soloDecimales } from "src/lib/entero-sin-signo";

function eventoDeTecla(key) {
  return { key, preventDefault: vi.fn() };
}

describe("bloquearNoEnteros", () => {
  it.each(["-", "+", "e", "E", ".", ","])("bloquea la tecla '%s'", (key) => {
    const e = eventoDeTecla(key);
    bloquearNoEnteros(e);
    expect(e.preventDefault).toHaveBeenCalledOnce();
  });

  it.each(["0", "5", "9", "Backspace", "ArrowLeft", "Tab"])("no bloquea '%s'", (key) => {
    const e = eventoDeTecla(key);
    bloquearNoEnteros(e);
    expect(e.preventDefault).not.toHaveBeenCalled();
  });
});

describe("soloDigitos", () => {
  it("quita todo lo que no sea dígito", () => {
    expect(soloDigitos("12a3-4")).toBe("1234");
  });

  it("quita un signo negativo pegado (pegar un número negativo)", () => {
    expect(soloDigitos("-5")).toBe("5");
  });

  it("quita el punto decimal -- esta función es solo para enteros", () => {
    expect(soloDigitos("3.14")).toBe("314");
  });

  it("una cadena de solo dígitos queda igual", () => {
    expect(soloDigitos("2026")).toBe("2026");
  });

  it("cadena vacía da cadena vacía", () => {
    expect(soloDigitos("")).toBe("");
  });

  it("acepta un número (lo convierte a texto antes de filtrar)", () => {
    expect(soloDigitos(123)).toBe("123");
  });
});

describe("bloquearNegativos", () => {
  it.each(["-", "+", "e", "E"])("bloquea la tecla '%s'", (key) => {
    const e = eventoDeTecla(key);
    bloquearNegativos(e);
    expect(e.preventDefault).toHaveBeenCalledOnce();
  });

  it.each(["." , ","])("NO bloquea '%s' -- a diferencia de bloquearNoEnteros, acá el punto decimal debe pasar", (key) => {
    const e = eventoDeTecla(key);
    bloquearNegativos(e);
    expect(e.preventDefault).not.toHaveBeenCalled();
  });

  it("no bloquea dígitos", () => {
    const e = eventoDeTecla("7");
    bloquearNegativos(e);
    expect(e.preventDefault).not.toHaveBeenCalled();
  });
});

describe("soloDecimales", () => {
  it("quita el signo negativo pero conserva el punto", () => {
    expect(soloDecimales("-12.5")).toBe("12.5");
  });

  it("quita letras y otros símbolos", () => {
    expect(soloDecimales("12.5e3")).toBe("12.53");
  });

  it("la coma no es un separador decimal reconocido: se quita, no se convierte en punto", () => {
    expect(soloDecimales("12,5")).toBe("125");
  });

  it("no deduplica varios puntos -- deja pasar los dos si el texto pegado trae dos", () => {
    // Comportamiento actual, no necesariamente el deseado: esta función solo
    // filtra caracteres, no valida que el resultado sea un número.
    expect(soloDecimales("1.2.3")).toBe("1.2.3");
  });

  it("cadena vacía da cadena vacía", () => {
    expect(soloDecimales("")).toBe("");
  });
});

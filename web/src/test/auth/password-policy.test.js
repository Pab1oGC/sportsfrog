import { describe, expect, it } from "vitest";
import { cumplePolitica, LARGO_MINIMO, MENSAJE_POLITICA } from "src/auth/password-policy";

// Las mismas cinco condiciones que Password.SatisfiesPolicy del lado del
// servidor. Esto no reemplaza esa validación -- la adelanta -- y estos casos
// son los que mantienen las dos diciendo lo mismo.
describe("cumplePolitica", () => {
  it("acepta una contraseña con las cinco condiciones", () => {
    expect(cumplePolitica("Nueva-Clave-2026!")).toBe(true);
  });

  it.each([
    ["corta de más", "Ab1!xyz"],
    ["sin mayúscula", "nueva-clave-2026!"],
    ["sin minúscula", "NUEVA-CLAVE-2026!"],
    ["sin número", "Nueva-Clave-Mia!"],
    ["sin carácter especial", "NuevaClave2026"],
  ])("rechaza una %s", (_caso, valor) => {
    expect(cumplePolitica(valor)).toBe(false);
  });

  it("no cuenta un espacio como carácter especial", () => {
    // La misma aclaración que lleva escrita Password.IsSpecial: sin excluir
    // el espacio, "no es letra ni dígito" lo aceptaría.
    expect(cumplePolitica("Nueva Clave 2026")).toBe(false);
  });

  it("acepta mayúsculas y minúsculas acentuadas, como char.IsUpper/IsLower", () => {
    expect(cumplePolitica("Ñandú-2026!")).toBe(true);
  });

  it("rechaza lo que no es un string", () => {
    expect(cumplePolitica(undefined)).toBe(false);
    expect(cumplePolitica(null)).toBe(false);
    expect(cumplePolitica(12345678)).toBe(false);
  });

  it("el largo mínimo que anuncia el mensaje es el que se exige", () => {
    expect(MENSAJE_POLITICA).toContain(String(LARGO_MINIMO));
    expect(cumplePolitica("Ab1!" + "x".repeat(LARGO_MINIMO - 5))).toBe(false);
    expect(cumplePolitica("Ab1!" + "x".repeat(LARGO_MINIMO - 4))).toBe(true);
  });
});

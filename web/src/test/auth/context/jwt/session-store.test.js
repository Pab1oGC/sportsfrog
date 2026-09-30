import { describe, expect, it, beforeEach } from "vitest";
import { guardarRenovacion, leerRenovacion, seRecuerda, olvidarRenovacion } from "src/auth/context/jwt/session-store";

// enMemoria es una variable de módulo compartida por todas las pruebas de
// este archivo (Vitest solo la reinicia entre archivos, no entre tests) --
// se limpia a mano antes de cada una para que no hereden estado.
beforeEach(() => {
  olvidarRenovacion();
});

describe("guardarRenovacion / leerRenovacion", () => {
  it("sin 'recordarme', el token se puede leer de inmediato (queda en memoria)", () => {
    guardarRenovacion("token-1", false);
    expect(leerRenovacion()).toBe("token-1");
  });

  it("sin 'recordarme', el token NO se persiste en localStorage", () => {
    guardarRenovacion("token-1", false);
    expect(localStorage.getItem("refresh_token")).toBeNull();
  });

  it("con 'recordarme', el token se persiste en localStorage además de quedar en memoria", () => {
    guardarRenovacion("token-1", true);
    expect(localStorage.getItem("refresh_token")).toBe("token-1");
    expect(leerRenovacion()).toBe("token-1");
  });

  it("con 'recordarme' mal seteado (token vacío), no persiste nada -- el `&& token` del guard lo evita", () => {
    guardarRenovacion("", true);
    expect(localStorage.getItem("refresh_token")).toBeNull();
  });

  it("bajar de 'recordado' a 'no recordado' purga el token viejo de localStorage", () => {
    // Guardar con recordar=true dos veces, la segunda con recordar=false: el
    // segundo guardado debe dejar localStorage limpio, no con el valor previo.
    guardarRenovacion("token-viejo", true);
    guardarRenovacion("token-nuevo", false);
    expect(localStorage.getItem("refresh_token")).toBeNull();
    expect(localStorage.getItem("remember_session")).toBeNull();
    // pero sigue disponible en memoria para la pestaña actual.
    expect(leerRenovacion()).toBe("token-nuevo");
  });

  it("sin nada guardado todavía, leerRenovacion da null", () => {
    expect(leerRenovacion()).toBeNull();
  });

  it("leerRenovacion prioriza la memoria sobre localStorage", () => {
    localStorage.setItem("refresh_token", "token-de-localstorage");
    guardarRenovacion("token-de-memoria", false);
    expect(leerRenovacion()).toBe("token-de-memoria");
  });

  it("si la memoria está vacía (recién cargó la pestaña), cae a leer localStorage", () => {
    localStorage.setItem("refresh_token", "token-persistido");
    expect(leerRenovacion()).toBe("token-persistido");
  });
});

describe("seRecuerda", () => {
  it("false cuando nunca se marcó 'recordarme'", () => {
    expect(seRecuerda()).toBe(false);
  });

  it("true después de guardar con recordar=true", () => {
    guardarRenovacion("token-1", true);
    expect(seRecuerda()).toBe(true);
  });

  it("false después de guardar con recordar=false", () => {
    guardarRenovacion("token-1", false);
    expect(seRecuerda()).toBe(false);
  });

  it("es estricto: cualquier otro valor guardado en la clave no cuenta como 'recordado'", () => {
    localStorage.setItem("remember_session", "true"); // no es el literal '1' exacto
    expect(seRecuerda()).toBe(false);
  });
});

describe("olvidarRenovacion", () => {
  it("borra la memoria y las dos claves de localStorage", () => {
    guardarRenovacion("token-1", true);
    olvidarRenovacion();
    expect(leerRenovacion()).toBeNull();
    expect(localStorage.getItem("refresh_token")).toBeNull();
    expect(localStorage.getItem("remember_session")).toBeNull();
  });

  it("no revienta si se llama sin haber guardado nada antes", () => {
    expect(() => olvidarRenovacion()).not.toThrow();
  });
});

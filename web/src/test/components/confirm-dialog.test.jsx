import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ConfirmProvider, useConfirm } from "src/components/confirm-dialog";

/** Botón que dispara confirm() y expone lo que resolvió, para poder afirmar sobre eso. */
function Probe({ mensaje = "¿Seguro?", opciones, onResult }) {
  const confirm = useConfirm();
  return (
    <button
      onClick={async () => {
        const resultado = await confirm(mensaje, opciones);
        onResult(resultado);
      }}
    >
      pedir
    </button>
  );
}

function montar(props) {
  const onResult = vi.fn();
  render(
    <ConfirmProvider>
      <Probe {...props} onResult={onResult} />
    </ConfirmProvider>,
  );
  return onResult;
}

describe("useConfirm fuera de ConfirmProvider", () => {
  it("lanza en vez de devolver un confirm silenciosamente roto", () => {
    const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});
    try {
      expect(() => render(<Probe onResult={() => {}} />)).toThrow(
        "useConfirm must be used within ConfirmProvider",
      );
    } finally {
      consoleSpy.mockRestore();
    }
  });
});

describe("ConfirmProvider / useConfirm", () => {
  it("confirmar resuelve true", async () => {
    const user = userEvent.setup();
    const onResult = montar({});
    await user.click(screen.getByRole("button", { name: "pedir" }));
    await user.click(screen.getByRole("button", { name: "Confirmar" }));
    expect(onResult).toHaveBeenCalledWith(true);
  });

  it("cancelar resuelve false", async () => {
    const user = userEvent.setup();
    const onResult = montar({});
    await user.click(screen.getByRole("button", { name: "pedir" }));
    await user.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(onResult).toHaveBeenCalledWith(false);
  });

  it("cerrar sin elegir (Escape) también resuelve false, no deja la promesa colgada", async () => {
    const user = userEvent.setup();
    const onResult = montar({});
    await user.click(screen.getByRole("button", { name: "pedir" }));
    await user.keyboard("{Escape}");
    expect(onResult).toHaveBeenCalledWith(false);
  });

  it("muestra el mensaje pasado a confirm()", async () => {
    const user = userEvent.setup();
    montar({ mensaje: "Eliminar club?" });
    await user.click(screen.getByRole("button", { name: "pedir" }));
    expect(screen.getByText("Eliminar club?")).toBeInTheDocument();
  });

  it("título, y etiquetas de confirmar/cancelar personalizados reemplazan a los de por defecto", async () => {
    const user = userEvent.setup();
    montar({ opciones: { title: "Zona peligrosa", confirmLabel: "Sí, eliminar", cancelLabel: "No, dejarlo" } });
    await user.click(screen.getByRole("button", { name: "pedir" }));

    expect(screen.getByText("Zona peligrosa")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sí, eliminar" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "No, dejarlo" })).toBeInTheDocument();
  });

  it("sin título propio, usa 'Confirmar' por defecto", async () => {
    const user = userEvent.setup();
    montar({});
    await user.click(screen.getByRole("button", { name: "pedir" }));
    expect(screen.getByRole("heading", { name: "Confirmar" })).toBeInTheDocument();
  });

  it("danger:true pinta el botón de confirmar como error, no como el color primario de siempre", async () => {
    const user = userEvent.setup();
    montar({ opciones: { danger: true } });
    await user.click(screen.getByRole("button", { name: "pedir" }));
    const botonConfirmar = screen.getByRole("button", { name: "Confirmar" });
    expect(botonConfirmar.className).toMatch(/error/i);
  });

  it("sin danger, el botón de confirmar usa el color primario, no error", async () => {
    const user = userEvent.setup();
    montar({});
    await user.click(screen.getByRole("button", { name: "pedir" }));
    const botonConfirmar = screen.getByRole("button", { name: "Confirmar" });
    expect(botonConfirmar.className).not.toMatch(/error/i);
  });

  it("ARREGLADO: llamar confirm() una segunda vez antes de resolver la primera ya no pisa su promesa -- se encola y se muestra después", async () => {
    // Reproduce el caso real: dos remove() disparados casi juntos (dos filas
    // borradas rápido) llaman confirm() dos veces antes de que la persona
    // llegue a contestar la primera pregunta. Un solo disparador -- no dos
    // botones separados -- porque una vez abierto el diálogo, MUI marca el
    // resto de la página aria-hidden y ya no hay de dónde hacer un segundo
    // click real de todos modos.
    //
    // Antes, el segundo confirm() pisaba el `state` del primero y su promesa
    // quedaba colgada para siempre (ver ConfirmProvider: ahora usa una cola
    // en vez de un único `state`). Esta prueba reemplaza a la que fijaba ese
    // defecto -- documenta que las dos preguntas se contestan, en orden.
    function DisparadorDoble({ onResultA, onResultB }) {
      const confirm = useConfirm();
      return (
        <button
          onClick={() => {
            confirm("Primero").then(onResultA);
            confirm("Segundo").then(onResultB);
          }}
        >
          disparar-dos
        </button>
      );
    }

    const user = userEvent.setup();
    const onResultA = vi.fn();
    const onResultB = vi.fn();
    render(
      <ConfirmProvider>
        <DisparadorDoble onResultA={onResultA} onResultB={onResultB} />
      </ConfirmProvider>,
    );

    await user.click(screen.getByRole("button", { name: "disparar-dos" }));

    // La primera pregunta se muestra primero -- la segunda espera su turno.
    expect(screen.getByText("Primero")).toBeInTheDocument();
    expect(screen.queryByText("Segundo")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Confirmar" }));
    expect(onResultA).toHaveBeenCalledWith(true);

    // Contestada la primera, ahora aparece la segunda -- no se perdió.
    expect(await screen.findByText("Segundo")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(onResultB).toHaveBeenCalledWith(false);
  });
});

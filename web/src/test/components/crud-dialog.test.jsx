import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CrudDialog } from "src/components/crud-dialog";

function propsBase(overrides = {}) {
  return {
    open: true,
    editId: null,
    entityName: "club",
    onClose: vi.fn(),
    onSave: vi.fn(),
    children: <div>contenido del formulario</div>,
    ...overrides,
  };
}

describe("CrudDialog -- título según alta/edición y género", () => {
  it("al crear, masculino (por defecto): 'Nuevo club'", () => {
    render(<CrudDialog {...propsBase()} />);
    expect(screen.getByRole("heading", { name: "Nuevo club" })).toBeInTheDocument();
  });

  it("al crear, femenino: 'Nueva sede', no 'Nuevo sede'", () => {
    render(<CrudDialog {...propsBase({ entityName: "sede", entityGender: "f" })} />);
    expect(screen.getByRole("heading", { name: "Nueva sede" })).toBeInTheDocument();
  });

  it("al editar, siempre 'Editar', sin importar el género", () => {
    render(<CrudDialog {...propsBase({ editId: "7", entityGender: "f", entityName: "sede" })} />);
    expect(screen.getByRole("heading", { name: "Editar sede" })).toBeInTheDocument();
  });
});

describe("CrudDialog -- estado 'saving'", () => {
  it("saving=true deshabilita Cancelar y Guardar, y muestra el spinner en Guardar", () => {
    render(<CrudDialog {...propsBase({ saving: true })} />);
    expect(screen.getByRole("button", { name: "Cancelar" })).toBeDisabled();
    expect(screen.getByRole("button", { name: /Guardar/ })).toBeDisabled();
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("saving=false (o ausente) deja ambos botones habilitados y sin spinner", () => {
    render(<CrudDialog {...propsBase()} />);
    expect(screen.getByRole("button", { name: "Cancelar" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Guardar" })).toBeEnabled();
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });
});

describe("CrudDialog -- error", () => {
  it("sin error, no muestra ninguna alerta", () => {
    render(<CrudDialog {...propsBase()} />);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("con error, muestra una alerta con el mensaje", () => {
    render(<CrudDialog {...propsBase({ error: "El nombre ya está en uso." })} />);
    expect(screen.getByRole("alert")).toHaveTextContent("El nombre ya está en uso.");
  });

  it("ARREGLADO: el botón de descarte de la alerta solo oculta el mensaje, ya no cierra el diálogo", async () => {
    // Antes, Alert onClose={onClose} reusaba el MISMO onClose que Cancelar y
    // el backdrop -- descartar el error perdía el formulario cargado. Ahora
    // CrudDialog lleva su propio estado de "mensaje ya descartado".
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<CrudDialog {...propsBase({ error: "El nombre ya está en uso.", onClose })} />);

    await user.click(screen.getByRole("button", { name: /close/i }));

    expect(onClose).not.toHaveBeenCalled();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("un error nuevo, distinto del descartado, se muestra igual", () => {
    const { rerender } = render(<CrudDialog {...propsBase({ error: "El nombre ya está en uso." })} />);
    rerender(<CrudDialog {...propsBase({ error: "El correo no es válido." })} />);
    expect(screen.getByRole("alert")).toHaveTextContent("El correo no es válido.");
  });

  it("tras descartar un error, si se reintenta guardar (saving pasa a true) el mismo mensaje puede volver a mostrarse", async () => {
    const user = userEvent.setup();
    const { rerender } = render(<CrudDialog {...propsBase({ error: "El nombre ya está en uso." })} />);

    await user.click(screen.getByRole("button", { name: /close/i }));
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();

    // Arranca un nuevo intento de guardado (saving:true, sin error todavía)...
    rerender(<CrudDialog {...propsBase({ saving: true, error: "" })} />);
    // ...que vuelve a fallar con el mismo mensaje de antes.
    rerender(<CrudDialog {...propsBase({ saving: false, error: "El nombre ya está en uso." })} />);

    expect(screen.getByRole("alert")).toHaveTextContent("El nombre ya está en uso.");
  });
});

describe("CrudDialog -- children y acciones", () => {
  it("renderiza el contenido del formulario pasado como children", () => {
    render(<CrudDialog {...propsBase({ children: <input aria-label="Nombre" /> })} />);
    expect(screen.getByLabelText("Nombre")).toBeInTheDocument();
  });

  it("Cancelar invoca onClose", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<CrudDialog {...propsBase({ onClose })} />);
    await user.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it("Guardar invoca onSave", async () => {
    const user = userEvent.setup();
    const onSave = vi.fn();
    render(<CrudDialog {...propsBase({ onSave })} />);
    await user.click(screen.getByRole("button", { name: "Guardar" }));
    expect(onSave).toHaveBeenCalledOnce();
  });

  it("open=false no renderiza el diálogo", () => {
    render(<CrudDialog {...propsBase({ open: false })} />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { EstadoChip } from "src/components/estado-chip";

describe("EstadoChip -- las cuatro combinaciones de etiqueta", () => {
  it("activo, masculino (por defecto): 'Activo'", () => {
    render(<EstadoChip activo onClick={vi.fn()} />);
    expect(screen.getByText("Activo")).toBeInTheDocument();
  });

  it("activo, femenino: 'Activa', no 'Activo'", () => {
    render(<EstadoChip activo femenino onClick={vi.fn()} />);
    expect(screen.getByText("Activa")).toBeInTheDocument();
  });

  it("inactivo, masculino (por defecto): 'Inactivo'", () => {
    render(<EstadoChip activo={false} onClick={vi.fn()} />);
    expect(screen.getByText("Inactivo")).toBeInTheDocument();
  });

  it("inactivo, femenino: 'Inactiva', no 'Inactivo'", () => {
    render(<EstadoChip activo={false} femenino onClick={vi.fn()} />);
    expect(screen.getByText("Inactiva")).toBeInTheDocument();
  });
});

describe("EstadoChip -- la inversión del tooltip (el verbo, no el estado)", () => {
  it("con el chip 'activo', el tooltip por defecto es 'Desactivar' -- la acción que apaga", async () => {
    const user = userEvent.setup();
    render(<EstadoChip activo onClick={vi.fn()} />);
    await user.hover(screen.getByRole("button"));
    expect(await screen.findByText("Desactivar")).toBeInTheDocument();
  });

  it("con el chip 'inactivo', el tooltip por defecto es 'Activar' -- la acción que prende", async () => {
    const user = userEvent.setup();
    render(<EstadoChip activo={false} onClick={vi.fn()} />);
    await user.hover(screen.getByRole("button"));
    expect(await screen.findByText("Activar")).toBeInTheDocument();
  });
});

describe("EstadoChip -- labels/iconos/tooltips propios (caso 'Pública' en Competencias)", () => {
  it("onLabel/offLabel reemplazan a Activo/Inactivo cuando se pasan", () => {
    const { rerender } = render(<EstadoChip activo onLabel="Sí" offLabel="No" onClick={vi.fn()} />);
    expect(screen.getByText("Sí")).toBeInTheDocument();

    rerender(<EstadoChip activo={false} onLabel="Sí" offLabel="No" onClick={vi.fn()} />);
    expect(screen.getByText("No")).toBeInTheDocument();
  });

  it("onTooltip/offTooltip reemplazan a Desactivar/Activar cuando se pasan", async () => {
    const user = userEvent.setup();
    render(<EstadoChip activo onTooltip="Despublicar" offTooltip="Publicar" onClick={vi.fn()} />);
    await user.hover(screen.getByRole("button"));
    expect(await screen.findByText("Despublicar")).toBeInTheDocument();
  });
});

describe("EstadoChip -- interacción", () => {
  it("clickear el chip dispara onClick", async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(<EstadoChip activo onClick={onClick} />);
    await user.click(screen.getByRole("button"));
    expect(onClick).toHaveBeenCalledOnce();
  });
});

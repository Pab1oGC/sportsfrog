import { describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RowActionsMenu } from "src/components/row-actions-menu";

// Nota: el <Tooltip><span aria-label={...}><IconButton/></span></Tooltip> de
// este componente termina con el aria-label en el <span> que envuelve al
// botón, no en el <button> mismo -- por eso getByRole('button', {name}) no
// los encuentra por su etiqueta acá. Se los ubica por posición en cambio: los
// primarios se renderizan primero, en el orden dado, y el botón "..." va
// siempre al final.

describe("RowActionsMenu -- acciones primarias", () => {
  it("acepta una sola acción primaria (objeto) y la muestra como icono suelto", () => {
    const onClick = vi.fn();
    render(<RowActionsMenu primary={{ icon: "eva:edit-fill", label: "Editar", onClick }} />);
    expect(screen.getAllByRole("button")).toHaveLength(1);
  });

  it("acepta un arreglo de acciones primarias y las muestra todas sueltas", () => {
    render(
      <RowActionsMenu
        primary={[
          { icon: "eva:edit-fill", label: "Editar", onClick: vi.fn() },
          { icon: "eva:trash-2-outline", label: "Eliminar", onClick: vi.fn() },
        ]}
      />,
    );
    expect(screen.getAllByRole("button")).toHaveLength(2);
  });

  it("una acción primaria con hidden:true no se muestra", () => {
    render(
      <RowActionsMenu
        primary={[
          { icon: "eva:edit-fill", label: "Editar", onClick: vi.fn() },
          { icon: "eva:trash-2-outline", label: "Eliminar", onClick: vi.fn(), hidden: true },
        ]}
      />,
    );
    expect(screen.getAllByRole("button")).toHaveLength(1);
  });

  it("una acción primaria dispara su onClick directamente, sin pasar por ningún menú", async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(<RowActionsMenu primary={{ icon: "eva:edit-fill", label: "Editar", onClick }} />);
    await user.click(screen.getByRole("button"));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it("una acción primaria disabled usa disabledLabel como tooltip en vez de label", async () => {
    const user = userEvent.setup();
    render(
      <RowActionsMenu
        primary={{ icon: "eva:play-fill", label: "Iniciar", disabled: true, disabledLabel: "Faltan los dos equipos", onClick: vi.fn() }}
      />,
    );
    // El botón está disabled -- pointer-events:none real -- pero el span que
    // lo envuelve (necesario para que el Tooltip funcione con un hijo
    // disabled) sí recibe el hover.
    await user.hover(screen.getByRole("button").parentElement);
    expect(await screen.findByText("Faltan los dos equipos")).toBeInTheDocument();
  });

  it("sin disabled, el tooltip muestra label -- no disabledLabel, aunque se haya pasado uno", async () => {
    const user = userEvent.setup();
    render(
      <RowActionsMenu
        primary={{ icon: "eva:play-fill", label: "Iniciar", disabledLabel: "No debería verse esto", onClick: vi.fn() }}
      />,
    );
    await user.hover(screen.getByRole("button"));
    expect(await screen.findByText("Iniciar")).toBeInTheDocument();
    expect(screen.queryByText("No debería verse esto")).not.toBeInTheDocument();
  });

  it("sin primary, no muestra ningún icono suelto", () => {
    render(<RowActionsMenu actions={[{ icon: "eva:trash-2-outline", label: "Eliminar", onClick: vi.fn() }]} />);
    // El único botón visible es el "..." -- ninguno primario.
    expect(screen.getAllByRole("button")).toHaveLength(1);
  });
});

describe('RowActionsMenu -- el menú "..."', () => {
  it("sin ninguna acción secundaria, no muestra el botón '...'", () => {
    render(<RowActionsMenu primary={{ icon: "eva:edit-fill", label: "Editar", onClick: vi.fn() }} actions={[]} />);
    // El único botón visible es el primario.
    expect(screen.getAllByRole("button")).toHaveLength(1);
  });

  it("con al menos una acción secundaria, muestra el botón '...' y al abrirlo lista las acciones", async () => {
    const user = userEvent.setup();
    render(
      <RowActionsMenu
        actions={[
          { icon: "eva:trash-2-outline", label: "Eliminar", onClick: vi.fn() },
          { icon: "eva:calendar-outline", label: "Reprogramar", onClick: vi.fn() },
        ]}
      />,
    );
    const botones = screen.getAllByRole("button");
    expect(botones).toHaveLength(1); // solo el "..."
    await user.click(botones[0]);

    const menu = screen.getByRole("menu");
    expect(within(menu).getByText("Eliminar")).toBeInTheDocument();
    expect(within(menu).getByText("Reprogramar")).toBeInTheDocument();
  });

  it("una acción secundaria con hidden:true no aparece en el menú", async () => {
    const user = userEvent.setup();
    render(
      <RowActionsMenu
        actions={[
          { icon: "eva:trash-2-outline", label: "Eliminar", onClick: vi.fn() },
          { icon: "eva:calendar-outline", label: "Reprogramar", onClick: vi.fn(), hidden: true },
        ]}
      />,
    );
    await user.click(screen.getByRole("button"));
    expect(screen.queryByText("Reprogramar")).not.toBeInTheDocument();
  });

  it("hasRealActions: si todas las acciones visibles son separadores, NO muestra el botón '...'", () => {
    render(<RowActionsMenu actions={[{ divider: true }]} />);
    expect(screen.queryAllByRole("button")).toHaveLength(0);
  });

  it("un divider dibuja una línea, no un ítem de menú clickeable, y no cuenta para decidir si se muestra el '...'", async () => {
    const user = userEvent.setup();
    render(
      <RowActionsMenu
        actions={[
          { icon: "eva:trash-2-outline", label: "Eliminar", onClick: vi.fn() },
          { divider: true },
          { icon: "eva:calendar-outline", label: "Reprogramar", onClick: vi.fn() },
        ]}
      />,
    );
    await user.click(screen.getByRole("button"));
    const menu = screen.getByRole("menu");
    // Dos ítems reales, ningún tercer ítem para el divider.
    expect(within(menu).getAllByRole("menuitem")).toHaveLength(2);
    expect(menu.querySelector(".MuiDivider-root")).toBeInTheDocument();
  });

  it("elegir una acción del menú lo cierra e invoca su onClick", async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(<RowActionsMenu actions={[{ icon: "eva:trash-2-outline", label: "Eliminar", onClick }]} />);

    await user.click(screen.getByRole("button"));
    expect(screen.getByRole("menu")).toBeInTheDocument();

    await user.click(screen.getByRole("menuitem", { name: "Eliminar" }));

    expect(onClick).toHaveBeenCalledOnce();
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("run(): el cierre del menú (setAnchorEl(null)) se ordena en el código antes de invocar el callback, aunque el DOM tarde un tick en reflejarlo", async () => {
    // React no aplica el nuevo estado de forma síncrona dentro del propio
    // manejador de clic -- por eso el Menu real puede seguir "open" en el DOM
    // en el instante exacto en que corre onClick(). Lo que sí es cierto, y lo
    // que importa, es el orden de las dos líneas de run(): primero se
    // programa el cierre, después se llama al callback -- nunca al revés.
    const orden = [];
    const onClick = vi.fn(() => orden.push("callback"));
    render(<RowActionsMenu actions={[{ icon: "eva:trash-2-outline", label: "Eliminar", onClick }]} />);

    const user = userEvent.setup();
    await user.click(screen.getByRole("button"));
    await user.click(screen.getByRole("menuitem", { name: "Eliminar" }));

    expect(orden).toEqual(["callback"]);
    // Tras asentarse la actualización, el menú efectivamente terminó cerrado.
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("una acción secundaria disabled se renderiza inaccesible al puntero (Mui-disabled), no solo 'con menos opacidad'", async () => {
    render(<RowActionsMenu actions={[{ icon: "eva:trash-2-outline", label: "Eliminar", onClick: vi.fn(), disabled: true }]} />);
    const user = userEvent.setup();
    await user.click(screen.getByRole("button"));

    const item = screen.getByRole("menuitem", { name: "Eliminar" });
    expect(item).toHaveAttribute("aria-disabled", "true");
    expect(item.className).toMatch(/Mui-disabled/);
  });
});

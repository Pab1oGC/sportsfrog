import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "src/test/render";
import { SectionNav } from "src/pages/landing/section-nav";

const SECTIONS = [
  { id: "uno", navLabel: "Uno", navIcon: "mdi:numeric-1-box-outline" },
  { id: "dos", navLabel: "Dos", navIcon: "mdi:numeric-2-box-outline" },
];

beforeEach(() => {
  document.body.innerHTML = "";
});

describe("SectionNav", () => {
  it("muestra una pill por cada sección", () => {
    renderWithProviders(<SectionNav sections={SECTIONS} />);
    expect(screen.getByText("Uno")).toBeInTheDocument();
    expect(screen.getByText("Dos")).toBeInTheDocument();
  });

  it("al hacer click en una pill, hace scroll a la sección correspondiente", async () => {
    const user = userEvent.setup();
    const destino = document.createElement("div");
    destino.id = "dos";
    document.body.appendChild(destino);
    destino.scrollIntoView = vi.fn();

    renderWithProviders(<SectionNav sections={SECTIONS} />);
    await user.click(screen.getByText("Dos"));

    expect(destino.scrollIntoView).toHaveBeenCalledOnce();
  });

  it("no revienta si la sección todavía no tiene un elemento con ese id en el DOM", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SectionNav sections={SECTIONS} />);
    await user.click(screen.getByText("Uno"));
    // Sin aserción adicional: el punto de la prueba es que el click anterior no lanzó.
  });
});

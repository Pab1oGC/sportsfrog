import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithProviders } from "src/test/render";
import { SectionPanel } from "src/pages/landing/section-panel";

function sectionFixture(overrides = {}) {
  return {
    id: "reglamento",
    navLabel: "Reglamento",
    navIcon: "mdi:gavel",
    eyebrow: "Reglamento",
    title: ["Primera linea", "Segunda linea"],
    description: "Descripcion de la seccion.",
    subFeatures: [
      { icon: "mdi:shape-outline", title: "Sub uno", desc: "Desc uno" },
      { icon: "mdi:shield-check-outline", title: "Sub dos", desc: "Desc dos" },
      { icon: "mdi:lock-outline", title: "Sub tres", desc: "Desc tres" },
    ],
    insteadOf: "En vez de algo manual",
    ...overrides,
  };
}

describe("SectionPanel", () => {
  it("usa el id de la sección como ancla", () => {
    renderWithProviders(<SectionPanel section={sectionFixture()} index={0} />);
    expect(document.getElementById("reglamento")).toBeInTheDocument();
  });

  it("muestra la insignia, las dos líneas del título, la descripción y las tres sub-características", () => {
    renderWithProviders(<SectionPanel section={sectionFixture()} index={0} />);

    expect(screen.getByText("Reglamento")).toBeInTheDocument();
    expect(screen.getByText("Primera linea")).toBeInTheDocument();
    expect(screen.getByText("Segunda linea")).toBeInTheDocument();
    expect(screen.getByText("Descripcion de la seccion.")).toBeInTheDocument();
    expect(screen.getByText("Sub uno")).toBeInTheDocument();
    expect(screen.getByText("Sub dos")).toBeInTheDocument();
    expect(screen.getByText("Sub tres")).toBeInTheDocument();
  });

  it("muestra la línea de 'en vez de' cuando la sección la trae", () => {
    renderWithProviders(<SectionPanel section={sectionFixture()} index={0} />);
    expect(screen.getByText("En vez de algo manual")).toBeInTheDocument();
  });

  it("no rompe si la sección no trae 'insteadOf'", () => {
    const { insteadOf, ...sinInsteadOf } = sectionFixture();
    renderWithProviders(<SectionPanel section={sinInsteadOf} index={0} />);
    expect(screen.queryByText("En vez de algo manual")).not.toBeInTheDocument();
  });

  it("sin children, muestra el marcador de vista previa en construcción", () => {
    renderWithProviders(<SectionPanel section={sectionFixture()} index={0} />);
    expect(screen.getByText("Vista previa en construcción")).toBeInTheDocument();
  });

  it("con children, renderiza el gráfico real en vez del marcador", () => {
    renderWithProviders(
      <SectionPanel section={sectionFixture()} index={0}>
        <div data-testid="grafico-real">grafico</div>
      </SectionPanel>,
    );
    expect(screen.getByTestId("grafico-real")).toBeInTheDocument();
    expect(screen.queryByText("Vista previa en construcción")).not.toBeInTheDocument();
  });
});

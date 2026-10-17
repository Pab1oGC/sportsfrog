import { describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithProviders } from "src/test/render";

vi.mock("gsap", () => ({
  default: {
    context: (fn) => {
      fn();
      return { revert: vi.fn() };
    },
    from: vi.fn(),
    registerPlugin: vi.fn(),
  },
}));

import { PortalVisual } from "src/pages/landing/visuals/portal-visual";

describe("PortalVisual", () => {
  it("muestra la barra de navegador, el banner y la tabla sin romper", () => {
    renderWithProviders(<PortalVisual />);

    expect(screen.getByText("sportfrog.app/liga-test/apertura-2026")).toBeInTheDocument();
    expect(screen.getByText("Sin cuenta")).toBeInTheDocument();
    expect(screen.getByText("Copa Apertura 2026")).toBeInTheDocument();
    expect(screen.getByText("Contraste verificado")).toBeInTheDocument();
  });

  // Regresión: dos roles de la paleta (secondary.main y brand.bright) tienen
  // el mismo hex, y el picker de colores usaba ese hex como key de React --
  // con las dos coincidiendo, React avisaba "two children with the same key".
  it("el picker de colores no repite key de React entre swatches", () => {
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
    renderWithProviders(<PortalVisual />);
    const sameKeyWarning = errorSpy.mock.calls.some((call) => String(call[0]).includes("same key"));
    expect(sameKeyWarning).toBe(false);
  });
});

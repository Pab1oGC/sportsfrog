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

import { SetupVisual } from "src/pages/landing/visuals/setup-visual";

describe("SetupVisual", () => {
  it("muestra las formas de resultado y el formulario sin romper", () => {
    renderWithProviders(<SetupVisual />);

    expect(screen.getByText("Por puntos")).toBeInTheDocument();
    expect(screen.getByText("Mejor de sets")).toBeInTheDocument();
    expect(screen.getByText("Nuevo reglamento")).toBeInTheDocument();
    expect(screen.getByText("Reglas consistentes")).toBeInTheDocument();
  });
});

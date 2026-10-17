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

import { ResultsVisual } from "src/pages/landing/visuals/results-visual";

describe("ResultsVisual", () => {
  it("muestra la tabla y el panel de goleador sin romper", () => {
    renderWithProviders(<ResultsVisual />);

    expect(screen.getByText("Tabla de posiciones")).toBeInTheDocument();
    expect(screen.getByText("Atlético Sur")).toBeInTheDocument();
    expect(screen.getByText("Goleador")).toBeInTheDocument();
    expect(screen.getByText("J. Pérez")).toBeInTheDocument();
  });
});

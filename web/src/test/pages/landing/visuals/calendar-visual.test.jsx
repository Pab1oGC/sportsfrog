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

import { CalendarVisual } from "src/pages/landing/visuals/calendar-visual";

describe("CalendarVisual", () => {
  it("muestra las sedes, los horarios y la jornada sin romper", () => {
    renderWithProviders(<CalendarVisual />);

    expect(screen.getByText("Cancha 1")).toBeInTheDocument();
    expect(screen.getByText("Cancha 4")).toBeInTheDocument();
    expect(screen.getByText("09:00")).toBeInTheDocument();
    expect(screen.getByText("Jornada 3")).toBeInTheDocument();
  });
});

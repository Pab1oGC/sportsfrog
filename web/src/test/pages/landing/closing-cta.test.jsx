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

import { ClosingCta } from "src/pages/landing/closing-cta";

describe("ClosingCta", () => {
  it("muestra el titular y las dos llamadas a la acción, a los mismos destinos de siempre", () => {
    renderWithProviders(<ClosingCta />);

    expect(screen.getByText("Todo listo para tu próxima temporada")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Iniciar sesión/ })).toHaveAttribute("href", "/auth/jwt/sign-in");
    expect(screen.getByRole("link", { name: /Ver competencias en vivo/ })).toHaveAttribute("href", "/public");
  });
});

import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithProviders } from "src/test/render";
import { Hero } from "src/pages/landing/hero";

describe("Hero", () => {
  it("muestra el titular y las dos llamadas a la acción", () => {
    renderWithProviders(<Hero />);

    expect(screen.getByRole("heading", { name: "Organiza tu competencia sin perder la calma" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Iniciar sesión/ })).toHaveAttribute("href", "/auth/jwt/sign-in");
    expect(screen.getByRole("link", { name: /Ver competencias publicadas/ })).toHaveAttribute("href", "/public");
  });

  it("el fondo geométrico es decorativo y no entra al árbol de accesibilidad", () => {
    const { container } = renderWithProviders(<Hero />);
    const fondo = container.querySelector('[aria-hidden="true"]');
    expect(fondo).toBeInTheDocument();
    expect(fondo.querySelector("svg")).toBeInTheDocument();
  });
});

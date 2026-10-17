import { describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "src/test/render";
import { SiteFooter } from "src/pages/landing/site-footer";

describe("SiteFooter", () => {
  it("muestra un enlace de 'Producto' por cada sección real de content.js", () => {
    renderWithProviders(<SiteFooter />);

    expect(screen.getByRole("link", { name: "Competencias" })).toHaveAttribute("href", "#competencias");
    expect(screen.getByRole("link", { name: "Portal" })).toHaveAttribute("href", "#portal");
    expect(screen.getByRole("link", { name: "Credenciales" })).toHaveAttribute("href", "#credenciales");
    expect(screen.getByRole("link", { name: "Escuelas" })).toHaveAttribute("href", "#escuelas");
  });

  it("muestra los dos destinos reales de 'Cuenta'", () => {
    renderWithProviders(<SiteFooter />);

    expect(screen.getByRole("link", { name: "Iniciar sesión" })).toHaveAttribute("href", "/auth/jwt/sign-in");
    expect(screen.getByRole("link", { name: "Competiciones públicas" })).toHaveAttribute("href", "/public");
  });

  it("al hacer click en un enlace de 'Producto', hace scroll a esa sección", async () => {
    const user = userEvent.setup();
    const destino = document.createElement("div");
    destino.id = "escuelas";
    destino.scrollIntoView = vi.fn();
    document.body.appendChild(destino);

    renderWithProviders(<SiteFooter />);
    await user.click(screen.getByRole("link", { name: "Escuelas" }));

    expect(destino.scrollIntoView).toHaveBeenCalledOnce();
  });

  it("muestra el copyright con el año actual", () => {
    renderWithProviders(<SiteFooter />);
    const year = new Date().getFullYear();
    expect(screen.getByText(new RegExp(`© ${year} SportFrog`))).toBeInTheDocument();
  });
});

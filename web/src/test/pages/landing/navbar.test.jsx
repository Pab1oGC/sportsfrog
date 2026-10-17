import { describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "src/test/render";
import { LandingNavbar } from "src/pages/landing/navbar";

const { mockNavigate } = vi.hoisted(() => ({ mockNavigate: vi.fn() }));
vi.mock("react-router", async (importOriginal) => {
  const actual = await importOriginal();
  return { ...actual, useNavigate: () => mockNavigate };
});

describe("LandingNavbar", () => {
  it("muestra el wordmark y los dos destinos reales", () => {
    renderWithProviders(<LandingNavbar />);
    expect(screen.getByText("SportFrog")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Competiciones" })).toHaveAttribute("href", "/public");
    expect(screen.getByRole("link", { name: "Iniciar sesión" })).toHaveAttribute("href", "/auth/jwt/sign-in");
  });

  it("al hacer click en el logo, navega al inicio", async () => {
    const user = userEvent.setup();
    renderWithProviders(<LandingNavbar />);
    await user.click(screen.getByAltText("SportFrog"));
    expect(mockNavigate).toHaveBeenCalledWith("/");
  });
});

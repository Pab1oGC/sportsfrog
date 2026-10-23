import { describe, expect, it, vi, beforeEach } from "vitest";
import userEvent from "@testing-library/user-event";
import { renderWithProviders, screen } from "src/test/render";

const { navegar } = vi.hoisted(() => ({ navegar: vi.fn() }));

vi.mock("react-router", async (importOriginal) => {
  const real = await importOriginal();
  return { ...real, useNavigate: () => navegar };
});

vi.mock("src/auth/hooks", () => ({
  useAuthContext: () => ({
    user: { fullName: "Coach Test", email: "coach@club.test", organizations: [] },
    checkSession: vi.fn(),
  }),
}));

vi.mock("src/auth/context/jwt", async (importOriginal) => {
  const real = await importOriginal();
  return { ...real, signOut: vi.fn() };
});

import { Header } from "src/layouts/dashboard/header";

beforeEach(() => { navegar.mockReset(); });

describe("Header", () => {
  it("ofrece cambiar contraseña en el menú del usuario, junto a cerrar sesión", async () => {
    renderWithProviders(<Header onToggleNav={vi.fn()} />);

    await userEvent.click(screen.getByText("C")); // avatar: primera letra de fullName

    expect(screen.getByRole("menuitem", { name: /Cambiar contraseña/ })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: /Cerrar sesión/ })).toBeInTheDocument();
  });

  it("abre el diálogo de cambiar contraseña y cierra el menú", async () => {
    renderWithProviders(<Header onToggleNav={vi.fn()} />);

    await userEvent.click(screen.getByText("C"));
    await userEvent.click(screen.getByRole("menuitem", { name: /Cambiar contraseña/ }));

    expect(screen.getByRole("dialog", { name: /Cambiar contraseña/ })).toBeInTheDocument();
    // El menú se cierra al abrir el diálogo, no se superponen.
    expect(screen.queryByRole("menuitem", { name: /Cerrar sesión/ })).not.toBeInTheDocument();
  });
});

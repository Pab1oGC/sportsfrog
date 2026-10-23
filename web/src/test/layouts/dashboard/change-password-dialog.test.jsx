import { describe, expect, it, vi, beforeEach } from "vitest";
import userEvent from "@testing-library/user-event";
import { renderWithProviders, screen, waitFor } from "src/test/render";

// Mismo motivo que en password-reset-pages.test.jsx: lo que importa al
// terminar es a donde manda, y eso depende de useNavigate y de que el
// contexto se haya releido antes.
const { navegar, revisarSesion } = vi.hoisted(() => ({ navegar: vi.fn(), revisarSesion: vi.fn() }));

vi.mock("react-router", async (importOriginal) => {
  const real = await importOriginal();
  return { ...real, useNavigate: () => navegar };
});

vi.mock("src/auth/hooks", () => ({
  useAuthContext: () => ({ checkSession: revisarSesion, authenticated: true, loading: false }),
}));

vi.mock("src/auth/context/jwt", async (importOriginal) => {
  const real = await importOriginal();
  return { ...real, changePassword: vi.fn() };
});

import { changePassword } from "src/auth/context/jwt";
import { ChangePasswordDialog } from "src/layouts/dashboard/change-password-dialog";

beforeEach(() => {
  navegar.mockReset();
  revisarSesion.mockReset();
  changePassword.mockReset();
});

describe("ChangePasswordDialog", () => {
  it("mantiene el botón deshabilitado si falta la contraseña actual", async () => {
    renderWithProviders(<ChangePasswordDialog open onClose={vi.fn()} />);

    await userEvent.type(screen.getByLabelText(/Contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.type(screen.getByLabelText(/Repetir la contraseña nueva/), "Nueva-Clave-2026!");

    expect(screen.getByRole("button", { name: /Cambiar la contraseña/ })).toBeDisabled();
    expect(changePassword).not.toHaveBeenCalled();
  });

  it("mantiene el botón deshabilitado mientras la nueva no cumple la política", async () => {
    renderWithProviders(<ChangePasswordDialog open onClose={vi.fn()} />);

    await userEvent.type(screen.getByLabelText(/Contraseña actual/), "Vieja-2026!");
    await userEvent.type(screen.getByLabelText(/Contraseña nueva/), "cortita");
    await userEvent.type(screen.getByLabelText(/Repetir la contraseña nueva/), "cortita");

    expect(screen.getByRole("button", { name: /Cambiar la contraseña/ })).toBeDisabled();
  });

  it("mantiene el botón deshabilitado si las dos contraseñas nuevas no coinciden", async () => {
    renderWithProviders(<ChangePasswordDialog open onClose={vi.fn()} />);

    await userEvent.type(screen.getByLabelText(/Contraseña actual/), "Vieja-2026!");
    await userEvent.type(screen.getByLabelText(/Contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.type(screen.getByLabelText(/Repetir la contraseña nueva/), "Otra-Clave-2026!");

    expect(screen.getByText("Las dos contraseñas tienen que ser iguales.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Cambiar la contraseña/ })).toBeDisabled();
  });

  it("cambia la contraseña, relee la sesión y vuelve al login con el aviso puesto", async () => {
    changePassword.mockResolvedValue(undefined);
    const onClose = vi.fn();

    renderWithProviders(<ChangePasswordDialog open onClose={onClose} />);

    await userEvent.type(screen.getByLabelText(/Contraseña actual/), "Vieja-2026!");
    await userEvent.type(screen.getByLabelText(/Contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.type(screen.getByLabelText(/Repetir la contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.click(screen.getByRole("button", { name: /Cambiar la contraseña/ }));

    await waitFor(() => {
      expect(changePassword).toHaveBeenCalledWith({
        currentPassword: "Vieja-2026!",
        newPassword: "Nueva-Clave-2026!",
      });
    });

    // El contexto se relee antes de navegar -- la API ya cerró la sesión, y
    // sin esto el GuestGuard del login rebotaría al dashboard.
    expect(revisarSesion).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
    expect(navegar).toHaveBeenCalledWith("/auth/jwt/sign-in?contrasenaCambiada=1", { replace: true });
  });

  it("muestra el rechazo cuando la contraseña actual es incorrecta, y no navega", async () => {
    changePassword.mockRejectedValue(new Error("La contraseña actual es incorrecta."));

    renderWithProviders(<ChangePasswordDialog open onClose={vi.fn()} />);

    await userEvent.type(screen.getByLabelText(/Contraseña actual/), "mal");
    await userEvent.type(screen.getByLabelText(/Contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.type(screen.getByLabelText(/Repetir la contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.click(screen.getByRole("button", { name: /Cambiar la contraseña/ }));

    expect(await screen.findByText("La contraseña actual es incorrecta.")).toBeInTheDocument();
    expect(navegar).not.toHaveBeenCalled();
  });

  it("no se cierra con Escape mientras hay un pedido en curso", async () => {
    let resolverCambio;
    changePassword.mockReturnValue(new Promise((resolve) => { resolverCambio = resolve; }));
    const onClose = vi.fn();

    renderWithProviders(<ChangePasswordDialog open onClose={onClose} />);

    await userEvent.type(screen.getByLabelText(/Contraseña actual/), "Vieja-2026!");
    await userEvent.type(screen.getByLabelText(/Contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.type(screen.getByLabelText(/Repetir la contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.click(screen.getByRole("button", { name: /Cambiar la contraseña/ }));

    // El botón "Cancelar" ya queda deshabilitado mientras carga; lo que
    // falta cubrir es la otra salida que el Dialog de MUI ofrece por su
    // cuenta -- Escape -- que llega al mismo cerrar() y tiene que respetar
    // la misma guarda.
    await userEvent.keyboard("{Escape}");
    expect(onClose).not.toHaveBeenCalled();

    resolverCambio();
  });
});

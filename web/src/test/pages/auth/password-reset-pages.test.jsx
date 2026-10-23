import { describe, expect, it, vi, beforeEach } from "vitest";
import userEvent from "@testing-library/user-event";
import { renderWithProviders, screen, waitFor } from "src/test/render";

// useNavigate se reemplaza porque lo que importa de reset-password al
// terminar es justamente a dónde manda: la API revocó todas las sesiones, y
// volver al login con el aviso puesto es la única salida correcta.
const { navegar, revisarSesion } = vi.hoisted(() => ({ navegar: vi.fn(), revisarSesion: vi.fn() }));

vi.mock("react-router", async (importOriginal) => {
  const real = await importOriginal();
  return { ...real, useNavigate: () => navegar };
});

// Sin AuthProvider en el árbol de prueba (renderWithProviders no lo monta
// por defecto), useAuthContext no tendría de dónde leer. Lo que la prueba
// necesita verificar es que la pantalla lo llame antes de volver al login.
vi.mock("src/auth/hooks", () => ({
  useAuthContext: () => ({ checkSession: revisarSesion, authenticated: false, loading: false }),
}));

vi.mock("src/auth/context/jwt", async (importOriginal) => {
  const real = await importOriginal();
  return {
    ...real,
    requestPasswordReset: vi.fn(),
    resetPassword: vi.fn(),
  };
});

import { requestPasswordReset, resetPassword } from "src/auth/context/jwt";
import ForgotPasswordPage from "src/pages/forgot-password";
import ResetPasswordPage from "src/pages/reset-password";

beforeEach(() => {
  navegar.mockReset();
  revisarSesion.mockReset();
  requestPasswordReset.mockReset();
  resetPassword.mockReset();
});

describe("ForgotPasswordPage", () => {
  it("manda el correo escrito y confirma sin decir si la cuenta existe", async () => {
    requestPasswordReset.mockResolvedValue(undefined);

    renderWithProviders(<ForgotPasswordPage />, { route: "/auth/jwt/forgot-password" });

    await userEvent.type(screen.getByLabelText(/Correo electrónico/), "coach@club.test");
    await userEvent.click(screen.getByRole("button", { name: /Enviar enlace/ }));

    expect(requestPasswordReset).toHaveBeenCalledWith({ email: "coach@club.test" });

    // "Si hay una cuenta...": el mismo texto exista o no, que es todo el
    // punto de que la API conteste 202 en los dos casos.
    const confirmacion = await screen.findByText(/Si hay una cuenta registrada con ese correo/);
    expect(confirmacion).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Enviar enlace/ })).not.toBeInTheDocument();
  });

  it("muestra el error del pedido sin dar por enviado el enlace", async () => {
    requestPasswordReset.mockRejectedValue(new Error("Demasiadas solicitudes."));

    renderWithProviders(<ForgotPasswordPage />, { route: "/auth/jwt/forgot-password" });

    await userEvent.type(screen.getByLabelText(/Correo electrónico/), "coach@club.test");
    await userEvent.click(screen.getByRole("button", { name: /Enviar enlace/ }));

    expect(await screen.findByText("Demasiadas solicitudes.")).toBeInTheDocument();
    expect(screen.queryByText(/Si hay una cuenta registrada/)).not.toBeInTheDocument();
  });
});

describe("ResetPasswordPage", () => {
  const conToken = { route: "/auth/jwt/reset-password?token=t-1" };

  it("sin token en la dirección, no muestra formulario sino cómo pedir uno nuevo", () => {
    renderWithProviders(<ResetPasswordPage />, { route: "/auth/jwt/reset-password" });

    expect(screen.getByText(/Este enlace está incompleto/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/Contraseña nueva/)).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Pedir un enlace nuevo/ }))
      .toHaveAttribute("href", "/auth/jwt/forgot-password");
  });

  it("mantiene el botón deshabilitado mientras la contraseña no cumple la política", async () => {
    renderWithProviders(<ResetPasswordPage />, conToken);

    const boton = screen.getByRole("button", { name: /Cambiar la contraseña/ });
    expect(boton).toBeDisabled();

    await userEvent.type(screen.getByLabelText(/Contraseña nueva/), "cortita");
    await userEvent.type(screen.getByLabelText(/Repetir la contraseña/), "cortita");

    expect(boton).toBeDisabled();
    expect(resetPassword).not.toHaveBeenCalled();
  });

  it("mantiene el botón deshabilitado si las dos contraseñas no coinciden", async () => {
    renderWithProviders(<ResetPasswordPage />, conToken);

    await userEvent.type(screen.getByLabelText(/Contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.type(screen.getByLabelText(/Repetir la contraseña/), "Nueva-Clave-2025!");

    expect(screen.getByText("Las dos contraseñas tienen que ser iguales.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Cambiar la contraseña/ })).toBeDisabled();
  });

  it("canjea el token del query string y vuelve al login con el aviso puesto", async () => {
    resetPassword.mockResolvedValue(undefined);

    renderWithProviders(<ResetPasswordPage />, conToken);

    await userEvent.type(screen.getByLabelText(/Contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.type(screen.getByLabelText(/Repetir la contraseña/), "Nueva-Clave-2026!");
    await userEvent.click(screen.getByRole("button", { name: /Cambiar la contraseña/ }));

    await waitFor(() => {
      expect(resetPassword).toHaveBeenCalledWith({ token: "t-1", password: "Nueva-Clave-2026!" });
    });

    // Primero deja el contexto consistente con la sesión que ya no existe,
    // o el GuestGuard del login rebota al dashboard y el aviso no se ve.
    expect(revisarSesion).toHaveBeenCalled();
    expect(navegar).toHaveBeenCalledWith("/auth/jwt/sign-in?contrasenaCambiada=1", { replace: true });
  });

  it("muestra el rechazo de un enlace vencido y deja pedir otro", async () => {
    resetPassword.mockRejectedValue(
      new Error("El enlace de recuperación no es válido o ya venció. Pedí uno nuevo."));

    renderWithProviders(<ResetPasswordPage />, conToken);

    await userEvent.type(screen.getByLabelText(/Contraseña nueva/), "Nueva-Clave-2026!");
    await userEvent.type(screen.getByLabelText(/Repetir la contraseña/), "Nueva-Clave-2026!");
    await userEvent.click(screen.getByRole("button", { name: /Cambiar la contraseña/ }));

    expect(await screen.findByText(/no es válido o ya venció/)).toBeInTheDocument();
    expect(navegar).not.toHaveBeenCalled();
    expect(screen.getByRole("link", { name: /Pedir un enlace nuevo/ })).toBeInTheDocument();
  });
});

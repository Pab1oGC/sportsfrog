import { describe, expect, it } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router";
import { AuthContext } from "src/auth/context/auth-context";
import { GuestGuard } from "src/auth/guard/guest-guard";

// Espejo de auth-guard.test.jsx, pero para la puerta de invitado: protege
// /auth/jwt/sign-in de quien ya tiene sesión, en vez de proteger el panel de
// quien no la tiene.
function montarConContexto({ authenticated, loading }) {
  return render(
    <AuthContext value={{ authenticated, loading }}>
      <MemoryRouter initialEntries={["/auth/jwt/sign-in"]}>
        <Routes>
          <Route path="/auth/jwt/sign-in" element={<GuestGuard>Formulario de inicio de sesión</GuestGuard>} />
          <Route path="/dashboard" element={<div>Panel</div>} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("GuestGuard", () => {
  it("mientras loading=true, muestra la pantalla de carga sin importar authenticated", () => {
    montarConContexto({ authenticated: true, loading: true });
    expect(screen.getByText("Cargando...")).toBeInTheDocument();
    expect(screen.queryByText("Formulario de inicio de sesión")).not.toBeInTheDocument();
  });

  it("ya autenticado, redirige al panel en vez de mostrar el formulario de login", async () => {
    montarConContexto({ authenticated: true, loading: false });
    await waitFor(() => expect(screen.getByText("Panel")).toBeInTheDocument());
    expect(screen.queryByText("Formulario de inicio de sesión")).not.toBeInTheDocument();
  });

  it("sin autenticar, muestra el formulario de login", async () => {
    montarConContexto({ authenticated: false, loading: false });
    await waitFor(() => expect(screen.getByText("Formulario de inicio de sesión")).toBeInTheDocument());
  });
});

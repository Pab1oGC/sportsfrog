import { describe, expect, it } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router";
import { AuthContext } from "src/auth/context/auth-context";
import { AuthGuard } from "src/auth/guard/auth-guard";

// AuthContext se inyecta directo con el valor que cada prueba necesita, en
// vez de pasar por el AuthProvider real: acá interesa el comportamiento del
// guard ante cada combinación de authenticated/loading, no cómo se llega a
// ella (eso ya lo cubre auth-provider.test.jsx).
function montarConContexto({ authenticated, loading }) {
  return render(
    <AuthContext value={{ authenticated, loading }}>
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Routes>
          <Route path="/dashboard" element={<AuthGuard>Contenido protegido</AuthGuard>} />
          <Route path="/auth/jwt/sign-in" element={<div>Pantalla de inicio de sesión</div>} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("AuthGuard", () => {
  it("mientras loading=true, muestra la pantalla de carga sin importar authenticated", () => {
    montarConContexto({ authenticated: false, loading: true });
    expect(screen.getByText("Cargando...")).toBeInTheDocument();
    expect(screen.queryByText("Contenido protegido")).not.toBeInTheDocument();
  });

  it("autenticado y sin cargar, renderiza el contenido protegido", async () => {
    montarConContexto({ authenticated: true, loading: false });
    await waitFor(() => expect(screen.getByText("Contenido protegido")).toBeInTheDocument());
  });

  it("no autenticado y sin cargar, redirige a la pantalla de inicio de sesión", async () => {
    montarConContexto({ authenticated: false, loading: false });
    await waitFor(() => expect(screen.getByText("Pantalla de inicio de sesión")).toBeInTheDocument());
    expect(screen.queryByText("Contenido protegido")).not.toBeInTheDocument();
  });

  it("no mira roles ni permisos: 'authenticated: true' alcanza aunque no haya ningún otro dato de usuario", async () => {
    // Por diseño -- la autorización real la hace el servidor en cada
    // endpoint. Esta prueba deja explícito que el guard no filtra por nada
    // más, para que nadie asuma lo contrario más adelante.
    montarConContexto({ authenticated: true, loading: false });
    await waitFor(() => expect(screen.getByText("Contenido protegido")).toBeInTheDocument());
  });
});

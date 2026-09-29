import { render } from "@testing-library/react";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterDayjs } from "@mui/x-date-pickers/AdapterDayjs";
import { SWRConfig } from "swr";
import { MemoryRouter } from "react-router";
import { AppThemeProvider } from "src/theme";
import { ConfirmProvider } from "src/components/confirm-dialog";
import { AuthProvider } from "src/auth/context/jwt";

/**
 * Réplica del árbol real de app.jsx (AppThemeProvider → LocalizationProvider
 * con AdapterDayjs en español → ConfirmProvider → ... → RouterProvider), más
 * dos capas que la app de producción no tiene pero toda prueba necesita:
 *
 * - SWRConfig con provider: () => new Map() -- una caché de SWR nueva por
 *   render, para que una prueba nunca vea datos que dejó otra. dedupingInterval:
 *   0 porque el valor por defecto de SWR (2s) puede hacer que un segundo
 *   render dentro de la misma prueba reciba la respuesta cacheada del primero
 *   en vez de volver a pedirla.
 * - MemoryRouter en vez de RouterProvider+createBrowserRouter: nada de esto
 *   necesita una URL real de navegador, y MemoryRouter permite arrancar en
 *   cualquier ruta con `route`.
 *
 * Sin este árbol no renderiza nada que use useConfirm (confirm-dialog.jsx
 * lanza fuera del proveedor), ni DateField/DateTimeField/TimeField, ni
 * useApi/useCrudDialog (SWR sigue funcionando sin SWRConfig, pero comparte
 * caché global entre pruebas sin él).
 *
 * @param {object} [options]
 * @param {string} [options.route='/'] - Ruta inicial del MemoryRouter.
 * @param {boolean} [options.withAuth=false] - Envuelve además en AuthProvider,
 *   para pruebas de AuthGuard/GuestGuard o de pantallas que leen useAuthContext.
 *   Por defecto no: la mayoría de las pruebas no necesita que AuthProvider
 *   dispare su chequeo de sesión (que pega contra axios) al montar.
 */
export function renderWithProviders(ui, { route = "/", withAuth = false, ...renderOptions } = {}) {
  function Wrapper({ children }) {
    const body = withAuth ? <AuthProvider>{children}</AuthProvider> : children;

    return (
      <AppThemeProvider>
        <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="es">
          <ConfirmProvider>
            <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
              <MemoryRouter initialEntries={[route]}>{body}</MemoryRouter>
            </SWRConfig>
          </ConfirmProvider>
        </LocalizationProvider>
      </AppThemeProvider>
    );
  }

  return render(ui, { wrapper: Wrapper, ...renderOptions });
}

// Re-exporta todo testing-library/react para que un archivo de prueba
// importe screen/waitFor/etc. desde el mismo lugar que renderWithProviders.
export * from "@testing-library/react";

import { Toaster } from "sonner";
import { AuthProvider } from "src/auth/context/jwt";
import { Snackbar } from "src/components/snackbar";
import { ConfirmProvider } from "src/components/confirm-dialog";
import { RouterProvider, createBrowserRouter } from "react-router";
import { routesSection } from "src/routes/sections";
import { ColorModeProvider } from "src/theme";

const router = createBrowserRouter(routesSection);

export default function App() {
  return (
    <ColorModeProvider>
      <ConfirmProvider>
        <AuthProvider>
          <Snackbar />
          {/* sonner: toast.success/toast.error, usados en la mayoría de las
              páginas, no rendereaban nada sin este montaje. */}
          <Toaster position="top-right" richColors closeButton />
          <RouterProvider router={router} />
        </AuthProvider>
      </ConfirmProvider>
    </ColorModeProvider>
  );
}

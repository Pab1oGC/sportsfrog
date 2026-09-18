import { Toaster } from "sonner";
import dayjs from "dayjs";
import "dayjs/locale/es";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterDayjs } from "@mui/x-date-pickers/AdapterDayjs";
import { AuthProvider } from "src/auth/context/jwt";
import { Snackbar } from "src/components/snackbar";
import { ConfirmProvider } from "src/components/confirm-dialog";
import { RouterProvider, createBrowserRouter } from "react-router";
import { routesSection } from "src/routes/sections";
import { AppThemeProvider } from "src/theme";

const router = createBrowserRouter(routesSection);

// adapterLocale="es" fija el formato dd/mm/aaaa y los nombres de mes en
// espanol para todo DatePicker de la app (ver src/components/date-field.jsx),
// sin importar el idioma del navegador de quien lo abre.
dayjs.locale("es");

export default function App() {
  return (
    <AppThemeProvider>
      <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="es">
        <ConfirmProvider>
          <AuthProvider>
            <Snackbar />
            {/* sonner: toast.success/toast.error, usados en la mayoría de las
                páginas, no rendereaban nada sin este montaje. */}
            <Toaster position="top-right" richColors closeButton />
            <RouterProvider router={router} />
          </AuthProvider>
        </ConfirmProvider>
      </LocalizationProvider>
    </AppThemeProvider>
  );
}

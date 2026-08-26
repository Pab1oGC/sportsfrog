import { ThemeProvider, createTheme } from "@mui/material/styles";
import CssBaseline from "@mui/material/CssBaseline";
import { AuthProvider } from "src/auth/context/jwt";
import { Snackbar } from "src/components/snackbar";
import { RouterProvider, createBrowserRouter } from "react-router";
import { routesSection } from "src/routes/sections";

const router = createBrowserRouter(routesSection);

const theme = createTheme({
  palette: { primary: { main: "#1B8A2E" }, background: { default: "#f4f6f8" } },
  typography: { fontFamily: "Inter, sans-serif" },
  shape: { borderRadius: 12 },
  components: {
    MuiButton: { styleOverrides: { root: { textTransform: "none", fontWeight: 600 } } },
    MuiCard: { styleOverrides: { root: { boxShadow: "0 2px 12px rgba(0,0,0,0.08)" } } },
  },
});

export default function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <AuthProvider>
        <Snackbar />
        <RouterProvider router={router} />
      </AuthProvider>
    </ThemeProvider>
  );
}

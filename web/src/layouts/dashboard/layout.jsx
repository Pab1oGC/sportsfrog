import { useState } from "react";
import { useLocation } from "react-router";
import Box from "@mui/material/Box";
import { useTheme } from "@mui/material/styles";
import useMediaQuery from "@mui/material/useMediaQuery";
import { Header } from "./header";
import { NavVertical } from "./nav-vertical";
import { getNavData } from "./nav-data";
import { useAuthContext } from "src/auth/hooks";

// Discreta a propósito: 8px y 240ms es la diferencia entre "la página cambió
// sola" y "algo se está animando". Se apaga entera con prefers-reduced-motion.
const ENTRADA_PAGINA = {
  "@keyframes fadeInUp": {
    from: { opacity: 0, transform: "translateY(8px)" },
    to: { opacity: 1, transform: "translateY(0)" },
  },
  animation: "fadeInUp 0.24s ease-out",
  "@media (prefers-reduced-motion: reduce)": { animation: "none" },
};

export function DashboardLayout({ children }) {
  const theme = useTheme();
  const location = useLocation();
  const isDesktop = useMediaQuery(theme.breakpoints.up("md"));
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const { user } = useAuthContext();

  const isPlatformAdmin = Boolean(
    user?.organizations?.some((org) => org.slug === "frogtech-solutions")
  );

  // Abajo de "md" el drawer no puede ser permanente: no hay ancho que
  // restarle al contenido sin dejarlo inservible. Ahi el boton de hamburguesa
  // abre y cierra un overlay en vez de angostar la barra.
  const toggleNav = () => (isDesktop ? setCollapsed((p) => !p) : setMobileOpen((p) => !p));

  return (
    <Box sx={{ display: "flex", minHeight: "100vh" }}>
      <NavVertical
        data={getNavData(isPlatformAdmin)}
        collapsed={isDesktop && collapsed}
        isDesktop={isDesktop}
        mobileOpen={mobileOpen}
        onCloseMobile={() => setMobileOpen(false)}
        onToggle={toggleNav}
      />
      {/* El Drawer permanente ya ocupa su ancho dentro de este flex: un
          margin-left acá además del ancho del Drawer contaba el espacio dos
          veces y corría todo el contenido de más, dejando una franja vacía. */}
      <Box sx={{ flexGrow: 1, minWidth: 0, display: "flex", flexDirection: "column" }}>
        <Header onToggleNav={toggleNav} />
        <Box component="main" key={location.pathname} sx={{ flexGrow: 1, p: { xs: 2, sm: 3 }, mt: "64px", ...ENTRADA_PAGINA }}>{children}</Box>
      </Box>
    </Box>
  );
}

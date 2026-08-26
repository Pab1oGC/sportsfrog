import { useState } from "react";
import Box from "@mui/material/Box";
import { Header } from "./header";
import { NavVertical } from "./nav-vertical";
import { navData } from "./nav-data";

export function DashboardLayout({ children }) {
  const [collapsed, setCollapsed] = useState(false);
  return (
    <Box sx={{ display: "flex", minHeight: "100vh" }}>
      <NavVertical data={navData} collapsed={collapsed} onToggle={() => setCollapsed(p => !p)} />
      <Box sx={{ flexGrow: 1, display: "flex", flexDirection: "column", ml: collapsed ? "72px" : "260px", transition: "margin-left 0.2s" }}>
        <Header onToggleNav={() => setCollapsed(p => !p)} />
        <Box component="main" sx={{ flexGrow: 1, p: { xs: 2, sm: 3 }, mt: "64px" }}>{children}</Box>
      </Box>
    </Box>
  );
}

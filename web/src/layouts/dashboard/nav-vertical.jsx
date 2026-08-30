import { useState } from "react";
import Box from "@mui/material/Box";
import Drawer from "@mui/material/Drawer";
import Collapse from "@mui/material/Collapse";
import Typography from "@mui/material/Typography";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemIcon from "@mui/material/ListItemIcon";
import ListItemText from "@mui/material/ListItemText";
import { useLocation, useNavigate } from "react-router";
import { Iconify } from "src/components/iconify";
import { Scrollbar } from "src/components/scrollbar";

export function NavVertical({ data, collapsed, isDesktop, mobileOpen, onCloseMobile, onToggle }) {
  const location = useLocation();
  const navigate = useNavigate();
  const [openSub, setOpenSub] = useState({});

  // "/dashboard" es prefijo de cualquier otra ruta del panel, así que solo él
  // exige coincidencia exacta: si no, "Dashboard" queda marcado activo en
  // Clubes, Competiciones y todo lo demás a la vez.
  const isActive = (path) =>
    location.pathname === path || (path !== "/dashboard" && location.pathname.startsWith(path + "/"));

  const goTo = (path) => {
    navigate(path);
    // El overlay tapa el contenido en mobile: sin esto, navegar deja el menu
    // abierto sobre la pagina a la que se acaba de ir.
    if (!isDesktop) onCloseMobile?.();
  };

  const content = (
    <Scrollbar sx={{ height: 1 }}>
      <Box sx={{ p: 2, display: "flex", alignItems: "center", gap: 1, cursor: "pointer" }} onClick={onToggle}>
        <Box sx={{ width: 36, height: 36, borderRadius: 1.5, bgcolor: "primary.main", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
          <Typography sx={{ color: "white", fontWeight: 700, fontSize: 18 }}>SF</Typography>
        </Box>
        {!collapsed && <Typography variant="h6" fontWeight={700}>SportFrog</Typography>}
      </Box>
      {data.map((section, si) => (
        <Box key={si} sx={{ mt: 1 }}>
          {!collapsed && <Typography variant="caption" sx={{ px: 2, py: 1, display: "block", color: "text.disabled", fontWeight: 600, textTransform: "uppercase", fontSize: 11 }}>{section.subheader}</Typography>}
          {section.items.map((item) => {
            const hasChildren = item.children?.length > 0;
            const open = openSub[item.title];
            return (
              <Box key={item.title}>
                <ListItemButton
                  onClick={() => { if (hasChildren) setOpenSub(p => ({ ...p, [item.title]: !open })); else goTo(item.path); }}
                  selected={isActive(item.path)}
                  sx={{
                    minHeight: 44, borderRadius: 1, mx: 1, mb: 0.25, px: 1.5,
                    "&.Mui-selected": { "& .MuiListItemIcon-root": { color: "primary.main" } },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 32, transition: "color 0.15s ease" }}><Iconify icon={item.icon || "mdi:circle-outline"} width={20} /></ListItemIcon>
                  {!collapsed && <ListItemText primary={item.title} slotProps={{ primary: { fontSize: 14, fontWeight: isActive(item.path) ? 700 : 500, sx: { transition: "font-weight 0.15s ease" } } }} />}
                  {!collapsed && hasChildren && <Iconify icon={open ? "eva:arrow-ios-upward-fill" : "eva:arrow-ios-downward-fill"} width={16} sx={{ color: "text.disabled" }} />}
                </ListItemButton>
                {hasChildren && !collapsed && (
                  <Collapse in={open} timeout="auto" unmountOnExit>
                    <Box sx={{ pl: 5 }}>
                      {item.children.map((child) => (
                        <ListItemButton key={child.title} onClick={() => goTo(child.path)} selected={isActive(child.path)} sx={{ minHeight: 36, borderRadius: 1, mb: 0.25 }}>
                          <ListItemText primary={child.title} slotProps={{ primary: { fontSize: 13 } }} />
                        </ListItemButton>
                      ))}
                    </Box>
                  </Collapse>
                )}
              </Box>
            );
          })}
        </Box>
      ))}
    </Scrollbar>
  );

  if (!isDesktop) {
    // Temporary: vive por encima del contenido y arranca cerrado, en vez de
    // restarle ancho a una pantalla que ya no le sobra nada.
    return (
      <Drawer
        variant="temporary"
        open={mobileOpen}
        onClose={onCloseMobile}
        ModalProps={{ keepMounted: true }}
        sx={{ "& .MuiDrawer-paper": { width: 260, borderRight: "1px solid", borderColor: "divider", bgcolor: "background.paper" } }}
      >
        {content}
      </Drawer>
    );
  }

  return (
    <Drawer variant="permanent" sx={{ width: collapsed ? 72 : 260, "& .MuiDrawer-paper": { width: collapsed ? 72 : 260, transition: "width 0.2s", overflow: "hidden", borderRight: "1px solid", borderColor: "divider", bgcolor: "background.paper" } }}>
      {content}
    </Drawer>
  );
}

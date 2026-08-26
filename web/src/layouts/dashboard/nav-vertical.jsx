import { useState, useEffect } from "react";
import { useTheme } from "@mui/material/styles";
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

export function NavVertical({ data, collapsed, onToggle }) {
  const theme = useTheme();
  const location = useLocation();
  const navigate = useNavigate();
  const [openSub, setOpenSub] = useState({});

  const isActive = (path) => location.pathname === path || location.pathname.startsWith(path + "/");

  return (
    <Drawer variant="permanent" sx={{ width: collapsed ? 72 : 260, "& .MuiDrawer-paper": { width: collapsed ? 72 : 260, transition: "width 0.2s", overflow: "hidden", borderRight: "1px solid", borderColor: "divider", bgcolor: "background.paper" } }}>
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
                  <ListItemButton onClick={() => { if (hasChildren) setOpenSub(p => ({ ...p, [item.title]: !open })); else navigate(item.path); }} selected={isActive(item.path)} sx={{ minHeight: 44, borderRadius: 1, mx: 1, mb: 0.25, px: 1.5 }}>
                    <ListItemIcon sx={{ minWidth: 32 }}><Iconify icon={item.icon || "mdi:circle-outline"} width={20} /></ListItemIcon>
                    {!collapsed && <ListItemText primary={item.title} primaryTypographyProps={{ fontSize: 14, fontWeight: isActive(item.path) ? 700 : 500 }} />}
                    {!collapsed && hasChildren && <Iconify icon={open ? "eva:arrow-ios-upward-fill" : "eva:arrow-ios-downward-fill"} width={16} sx={{ color: "text.disabled" }} />}
                  </ListItemButton>
                  {hasChildren && !collapsed && (
                    <Collapse in={open} timeout="auto" unmountOnExit>
                      <Box sx={{ pl: 5 }}>
                        {item.children.map((child) => (
                          <ListItemButton key={child.title} onClick={() => navigate(child.path)} selected={isActive(child.path)} sx={{ minHeight: 36, borderRadius: 1, mb: 0.25 }}>
                            <ListItemText primary={child.title} primaryTypographyProps={{ fontSize: 13 }} />
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
    </Drawer>
  );
}

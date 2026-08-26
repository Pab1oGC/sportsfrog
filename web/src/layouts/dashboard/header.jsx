import { useState } from "react";
import Box from "@mui/material/Box";
import AppBar from "@mui/material/AppBar";
import Toolbar from "@mui/material/Toolbar";
import IconButton from "@mui/material/IconButton";
import Typography from "@mui/material/Typography";
import Avatar from "@mui/material/Avatar";
import Menu from "@mui/material/Menu";
import MenuItem from "@mui/material/MenuItem";
import Tooltip from "@mui/material/Tooltip";
import Chip from "@mui/material/Chip";
import { useNavigate } from "react-router";
import { Iconify } from "src/components/iconify";
import { signOut } from "src/auth/context/jwt";
import { useAuthContext } from "src/auth/hooks";

export function Header({ onToggleNav }) {
  const navigate = useNavigate();
  const { user } = useAuthContext();
  const [anchorEl, setAnchorEl] = useState(null);
  const orgs = user?.organizations || [];
  const currentOrg = orgs[0];

  return (
    <AppBar position="fixed" elevation={0} sx={{ bgcolor: "background.paper", color: "text.primary", borderBottom: "1px solid", borderColor: "divider", backdropFilter: "blur(8px)", zIndex: 1200 }}>
      <Toolbar sx={{ px: { xs: 1, sm: 3 } }}>
        <IconButton onClick={onToggleNav} sx={{ mr: 1 }}><Iconify icon="eva:menu-2-fill" /></IconButton>
        {currentOrg && <Chip label={currentOrg.name} color="primary" variant="outlined" size="small" sx={{ mr: 2 }} />}
        <Box sx={{ flexGrow: 1 }} />
        <Tooltip title={user?.email || ""}>
          <Avatar sx={{ bgcolor: "primary.main", cursor: "pointer" }} onClick={(e) => setAnchorEl(e.currentTarget)}>{(user?.fullName || "U")[0]}</Avatar>
        </Tooltip>
        <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={() => setAnchorEl(null)}>
          <MenuItem disabled><Typography variant="body2">{user?.fullName}</Typography></MenuItem>
          <MenuItem onClick={async () => { await signOut(); navigate("/auth/jwt/sign-in", { replace: true }); }}><Iconify icon="eva:log-out-fill" sx={{ mr: 1 }} />Cerrar sesión</MenuItem>
        </Menu>
      </Toolbar>
    </AppBar>
  );
}

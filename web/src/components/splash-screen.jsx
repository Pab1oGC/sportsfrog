import { useState } from "react";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Typography from "@mui/material/Typography";

export function SplashScreen() {
  return (
    <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", height: "100vh", gap: 2 }}>
      <CircularProgress />
      <Typography variant="body2" color="text.secondary">Cargando...</Typography>
    </Box>
  );
}

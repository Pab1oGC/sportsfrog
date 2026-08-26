import { useState, useCallback } from "react";
import { Snackbar as MuiSnackbar, Alert } from "@mui/material";

let showFn;
export function Snackbar({ children }) {
  const [open, setOpen] = useState(false);
  const [msg, setMsg] = useState("");
  const [severity, setSeverity] = useState("info");
  const show = useCallback((m, s = "info") => { setMsg(m); setSeverity(s); setOpen(true); }, []);
  showFn = show;
  return (
    <>
      {children}
      <MuiSnackbar open={open} autoHideDuration={4000} onClose={() => setOpen(false)} anchorOrigin={{ vertical: "top", horizontal: "right" }}>
        <Alert severity={severity} variant="filled" onClose={() => setOpen(false)}>{msg}</Alert>
      </MuiSnackbar>
    </>
  );
}
export const useSnackbar = () => ({ show: (m, s) => showFn?.(m, s) });

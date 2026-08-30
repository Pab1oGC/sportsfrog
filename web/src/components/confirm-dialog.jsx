import { createContext, useContext, useState, useCallback } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';

const ConfirmContext = createContext(null);

/**
 * Reemplaza a window.confirm: misma forma de uso (una pregunta, un booleano
 * de vuelta) pero como modal propio en vez de un popup nativo del navegador.
 *
 *   const ok = await confirm('Eliminar equipo?');
 *   if (!ok) return;
 */
export function useConfirm() {
  const ctx = useContext(ConfirmContext);
  if (!ctx) throw new Error('useConfirm must be used within ConfirmProvider');
  return ctx;
}

export function ConfirmProvider({ children }) {
  const [state, setState] = useState(null);

  const confirm = useCallback((message, options = {}) => {
    return new Promise((resolve) => {
      setState({ message, resolve, ...options });
    });
  }, []);

  const close = (result) => {
    state?.resolve(result);
    setState(null);
  };

  return (
    <ConfirmContext value={confirm}>
      {children}
      <Dialog open={!!state} onClose={() => close(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{state?.title || 'Confirmar'}</DialogTitle>
        <DialogContent>
          <DialogContentText>{state?.message}</DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => close(false)}>{state?.cancelLabel || 'Cancelar'}</Button>
          <Button
            variant="contained"
            color={state?.danger ? 'error' : 'primary'}
            onClick={() => close(true)}
            autoFocus
          >
            {state?.confirmLabel || 'Confirmar'}
          </Button>
        </DialogActions>
      </Dialog>
    </ConfirmContext>
  );
}

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
  // Una cola, no un solo `state`: con un solo state, un segundo confirm()
  // llamado antes de que la persona conteste el primero pisaba su `resolve`
  // sin dejar rastro -- esa primera promesa quedaba pendiente para siempre.
  // Encolando, cada pedido conserva su propio resolve y se muestra recién
  // cuando le toca el turno; el de adelante (queue[0]) es el que se ve.
  const [queue, setQueue] = useState([]);
  const state = queue[0] || null;

  const confirm = useCallback((message, options = {}) => {
    return new Promise((resolve) => {
      setQueue((q) => [...q, { message, resolve, ...options }]);
    });
  }, []);

  const close = (result) => {
    setQueue((q) => {
      const [actual, ...resto] = q;
      actual?.resolve(result);
      return resto;
    });
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

import { useState, useCallback } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import { useNavigate } from 'react-router';
import { changePassword } from 'src/auth/context/jwt';
import { useAuthContext } from 'src/auth/hooks';
import { cumplePolitica, MENSAJE_POLITICA } from 'src/auth/password-policy';

const VACIO = { currentPassword: '', newPassword: '', confirmacion: '' };

/**
 * Cambiar la contraseña sabiendo la actual, desde el menu del usuario.
 *
 * El unico otro camino para elegir una contraseña nueva es forgot-password.jsx
 * -- pedir un enlace por correo, para alguien que ya esta adentro y no la
 * olvido es un rodeo. Mismo final que ese y que reset-password.jsx: la API
 * revoca todas las sesiones al cambiarla (ver ChangePassword.cs), asi que no
 * hay sesion con la que seguir y se vuelve al login con el mismo aviso.
 */
export function ChangePasswordDialog({ open, onClose }) {
  const navigate = useNavigate();
  const { checkSession } = useAuthContext();

  const [form, setForm] = useState(VACIO);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const politicaOk = cumplePolitica(form.newPassword);
  const coinciden = form.newPassword.length > 0 && form.newPassword === form.confirmacion;
  const puedeEnviar = form.currentPassword.length > 0 && politicaOk && coinciden;

  const cerrar = useCallback(() => {
    if (loading) return; // No se cierra a mitad de un pedido en curso.
    setForm(VACIO); setError('');
    onClose();
  }, [loading, onClose]);

  const handleSubmit = useCallback(async (e) => {
    e.preventDefault();
    if (!puedeEnviar) return;
    setLoading(true); setError('');
    try {
      await changePassword({ currentPassword: form.currentPassword, newPassword: form.newPassword });
      // Mismo motivo que resetPassword: la sesion ya no existe del lado del
      // servidor, asi que checkSession() antes de navegar deja el contexto
      // consistente -- si no, el GuestGuard del login rebotaria al
      // dashboard con una sesion que la API ya cerro.
      await checkSession();
      setForm(VACIO);
      onClose();
      navigate('/auth/jwt/sign-in?contrasenaCambiada=1', { replace: true });
    } catch (err) {
      // 401 cuando la contraseña actual es incorrecta, 400 cuando la nueva
      // no cumple la politica -- la API ya trae el texto puesto para los dos.
      setError(err.message || 'No se pudo cambiar la contraseña.');
    } finally { setLoading(false); }
  }, [puedeEnviar, form, checkSession, onClose, navigate]);

  return (
    <Dialog open={open} onClose={cerrar} maxWidth="sm" fullWidth>
      <DialogTitle>Cambiar contraseña</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField
            label="Contraseña actual"
            type="password"
            value={form.currentPassword}
            onChange={(e) => setForm({ ...form, currentPassword: e.target.value })}
            fullWidth
            required
            autoFocus
          />
          <TextField
            label="Contraseña nueva"
            type="password"
            value={form.newPassword}
            onChange={(e) => setForm({ ...form, newPassword: e.target.value })}
            error={form.newPassword.length > 0 && !politicaOk}
            helperText={MENSAJE_POLITICA}
            fullWidth
            required
          />
          <TextField
            label="Repetir la contraseña nueva"
            type="password"
            value={form.confirmacion}
            onChange={(e) => setForm({ ...form, confirmacion: e.target.value })}
            error={form.confirmacion.length > 0 && !coinciden}
            helperText={form.confirmacion.length > 0 && !coinciden ? 'Las dos contraseñas tienen que ser iguales.' : ' '}
            fullWidth
            required
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={cerrar} disabled={loading}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={loading || !puedeEnviar}
            startIcon={loading ? <CircularProgress size={18} /> : null}>
            Cambiar la contraseña
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

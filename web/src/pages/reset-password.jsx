import { useState, useCallback, useMemo } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import TextField from '@mui/material/TextField';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Link from '@mui/material/Link';
import { useNavigate, useSearchParams, Link as RouterLink } from 'react-router';
import { resetPassword } from 'src/auth/context/jwt';
import { useAuthContext } from 'src/auth/hooks';
import { cumplePolitica, MENSAJE_POLITICA } from 'src/auth/password-policy';

/**
 * Donde termina el enlace del correo: elegir la contraseña nueva.
 *
 * El token viaja en el query string y es la unica credencial que esta
 * pantalla tiene -- no hay sesion detras, por eso cuelga de /auth y no del
 * dashboard. Tampoco va envuelta en GuestGuard, a diferencia de sign-in:
 * alguien con la sesion abierta que sospecha que le entraron a la cuenta
 * tiene que poder usar el enlace igual, y mandarlo al dashboard dejaria el
 * token sin usar. Al terminar no queda sesion de todos modos -- la API
 * revoca todas, y resetPassword() limpia lo que haya quedado en este
 * navegador.
 */
export default function ResetPasswordPage() {
  const navigate = useNavigate();
  const { checkSession } = useAuthContext();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') || '';

  const [password, setPassword] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const politicaOk = useMemo(() => cumplePolitica(password), [password]);
  const coinciden = password.length > 0 && password === confirmacion;

  const handleSubmit = useCallback(async (e) => {
    e.preventDefault();
    setLoading(true); setError('');
    try {
      await resetPassword({ token, password });
      // Quien llego aca con la sesion abierta ya no la tiene -- la API las
      // revoco todas y resetPassword() borro lo que quedaba guardado --, pero
      // el contexto todavia la da por viva, y asi el GuestGuard del login
      // rebotaria al dashboard y el aviso no se veria. checkSession() relee
      // lo que quedo (nada) y lo deja consistente, igual que hace sign-in
      // despues de iniciar sesion.
      await checkSession();
      navigate('/auth/jwt/sign-in?contrasenaCambiada=1', { replace: true });
    } catch (err) {
      // 401 cuando el enlace es desconocido, vencio o ya se uso -- un solo
      // mensaje para los tres, igual que lo contesta la API. 400 cuando la
      // contraseña no cumple la politica, que ya trae el texto puesto.
      setError(err.message || 'No se pudo cambiar la contraseña.');
    } finally { setLoading(false); }
  }, [token, password, navigate, checkSession]);

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.default', p: 2 }}>
      <Card sx={{ width: 420, maxWidth: '100%' }}>
        <CardContent sx={{ p: 4 }}>
          <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', mb: 3 }}>
            <Box sx={{ width: 56, height: 56, borderRadius: 2, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center', mb: 2, cursor: 'pointer' }}
              onClick={() => navigate('/')}>
              <Typography sx={{ color: 'primary.contrastText', fontWeight: 800, fontSize: 24 }}>SF</Typography>
            </Box>
            <Typography variant="h5" fontWeight={700}>Nueva contraseña</Typography>
            <Typography variant="body2" color="text.secondary">Elegí la que vas a usar de ahora en más</Typography>
          </Box>

          {token ? (
            <>
              {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

              <form onSubmit={handleSubmit}>
                <TextField
                  fullWidth
                  label="Contraseña nueva"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  // El mensaje se muestra siempre: de ayuda mientras se
                  // escribe, de error recien cuando hay algo escrito que no
                  // alcanza. Decirlo solo al fallar obliga a adivinar primero.
                  error={password.length > 0 && !politicaOk}
                  helperText={MENSAJE_POLITICA}
                  sx={{ mb: 2 }}
                  required
                  autoFocus
                />
                <TextField
                  fullWidth
                  label="Repetir la contraseña"
                  type="password"
                  value={confirmacion}
                  onChange={(e) => setConfirmacion(e.target.value)}
                  error={confirmacion.length > 0 && !coinciden}
                  helperText={confirmacion.length > 0 && !coinciden ? 'Las dos contraseñas tienen que ser iguales.' : ' '}
                  sx={{ mb: 2 }}
                  required
                />
                <Button fullWidth type="submit" variant="contained" size="large"
                  disabled={loading || !politicaOk || !coinciden}
                  startIcon={loading ? <CircularProgress size={20} /> : null}>
                  Cambiar la contraseña
                </Button>
              </form>

              <Typography variant="body2" color="text.secondary" sx={{ mt: 2, textAlign: 'center' }}>
                <Link component={RouterLink} to="/auth/jwt/forgot-password">Pedir un enlace nuevo</Link>
              </Typography>
            </>
          ) : (
            <>
              {/* Se llego sin token: el enlace se corto al copiarlo, o alguien
                  abrio la direccion a mano. No hay nada que canjear, y un
                  formulario que no puede funcionar es peor que no mostrarlo. */}
              <Alert severity="warning" sx={{ mb: 2 }}>
                Este enlace está incompleto. Pedí uno nuevo y abrilo directamente desde el correo.
              </Alert>
              <Button fullWidth variant="contained" size="large" component={RouterLink} to="/auth/jwt/forgot-password">
                Pedir un enlace nuevo
              </Button>
            </>
          )}
        </CardContent>
      </Card>
    </Box>
  );
}

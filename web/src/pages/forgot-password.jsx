import { useState, useCallback } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import TextField from '@mui/material/TextField';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Link from '@mui/material/Link';
import { useNavigate, Link as RouterLink } from 'react-router';
import { requestPasswordReset } from 'src/auth/context/jwt';

/**
 * Pide el enlace para volver a entrar sin saber la contraseña.
 *
 * Lo que la pantalla dice al terminar es deliberadamente el mismo mensaje
 * exista o no una cuenta con ese correo. La API ya contesta igual en los dos
 * casos (ver RequestPasswordReset.cs, que hasta encola el envío de las dos
 * maneras para que tampoco se distingan por lo que tardan); decir acá "te
 * mandamos un correo" contra "ese correo no está registrado" tiraría por la
 * borda todo eso desde el lado más visible.
 */
export default function ForgotPasswordPage() {
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [enviado, setEnviado] = useState(false);

  const handleSubmit = useCallback(async (e) => {
    e.preventDefault();
    setLoading(true); setError('');
    try {
      await requestPasswordReset({ email });
      setEnviado(true);
    } catch (err) {
      // Un 429 del limitador, o la red caída. Nada de esto habla de la
      // cuenta: son las dos formas que tiene de fallar el pedido en sí.
      setError(err.message || 'No se pudo enviar el pedido. Probá de nuevo en un momento.');
    } finally { setLoading(false); }
  }, [email]);

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.default', p: 2 }}>
      <Card sx={{ width: 420, maxWidth: '100%' }}>
        <CardContent sx={{ p: 4 }}>
          <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', mb: 3 }}>
            <Box sx={{ width: 56, height: 56, borderRadius: 2, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center', mb: 2, cursor: 'pointer' }}
              onClick={() => navigate('/')}>
              <Typography sx={{ color: 'primary.contrastText', fontWeight: 800, fontSize: 24 }}>SF</Typography>
            </Box>
            <Typography variant="h5" fontWeight={700}>Recuperar contraseña</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mt: 0.5 }}>
              {enviado
                ? 'Revisá tu correo'
                : 'Te enviamos un enlace para elegir una nueva'}
            </Typography>
          </Box>

          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

          {enviado ? (
            <>
              <Alert severity="success" sx={{ mb: 2 }}>
                Si hay una cuenta registrada con ese correo, el enlace ya está en camino.
                Vence en una hora y sirve una sola vez.
              </Alert>
              <Button fullWidth variant="contained" size="large" component={RouterLink} to="/auth/jwt/sign-in">
                Volver a iniciar sesión
              </Button>
            </>
          ) : (
            <form onSubmit={handleSubmit}>
              <TextField
                fullWidth
                label="Correo electrónico"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                sx={{ mb: 2 }}
                required
                autoFocus
              />
              <Button fullWidth type="submit" variant="contained" size="large" disabled={loading}
                startIcon={loading ? <CircularProgress size={20} /> : null}>
                Enviar enlace
              </Button>
            </form>
          )}

          <Typography variant="body2" color="text.secondary" sx={{ mt: 2, textAlign: 'center' }}>
            <Link component={RouterLink} to="/auth/jwt/sign-in">Volver a iniciar sesión</Link>
          </Typography>
        </CardContent>
      </Card>
    </Box>
  );
}

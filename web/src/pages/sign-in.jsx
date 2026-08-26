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
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import Tooltip from '@mui/material/Tooltip';
import { useNavigate, useSearchParams, Link as RouterLink } from 'react-router';
import { signIn } from 'src/auth/context/jwt';

export default function SignInPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const returnTo = searchParams.get('returnTo') || '/dashboard';

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [remember, setRemember] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = useCallback(async (e) => {
    e.preventDefault();
    setLoading(true); setError('');
    try {
      await signIn({ email, password, remember });
      navigate(returnTo, { replace: true });
      window.location.reload();
    } catch (err) { setError(err.message || 'Credenciales incorrectas'); }
    finally { setLoading(false); }
  }, [email, password, remember, navigate, returnTo]);

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'grey.100', p: 2 }}>
      <Card sx={{ width: 420, maxWidth: '100%' }}>
        <CardContent sx={{ p: 4 }}>
          <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', mb: 3 }}>
            <Box sx={{ width: 56, height: 56, borderRadius: 2, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center', mb: 2, cursor: 'pointer' }}
              onClick={() => navigate('/')}>
              <Typography sx={{ color: 'white', fontWeight: 800, fontSize: 24 }}>SF</Typography>
            </Box>
            <Typography variant="h5" fontWeight={700}>SportFrog</Typography>
            <Typography variant="body2" color="text.secondary">Inicia sesion en tu cuenta</Typography>
          </Box>

          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

          <form onSubmit={handleSubmit}>
            <TextField fullWidth label="Correo electronico" type="email" value={email} onChange={(e) => setEmail(e.target.value)} sx={{ mb: 2 }} required />
            <TextField fullWidth label="Contrasena" type="password" value={password} onChange={(e) => setPassword(e.target.value)} sx={{ mb: 2 }} required />
            <Tooltip title="Deja la sesion abierta en este navegador. No lo actives en una computadora compartida." placement="right">
              <FormControlLabel
                control={<Checkbox checked={remember} onChange={(e) => setRemember(e.target.checked)} size="small" />}
                label={<Typography variant="body2" color="text.secondary">Mantener sesion iniciada</Typography>}
                sx={{ mb: 2, mt: -1 }}
              />
            </Tooltip>
            <Button fullWidth type="submit" variant="contained" size="large" disabled={loading}
              startIcon={loading ? <CircularProgress size={20} /> : null}>
              Iniciar sesion
            </Button>
          </form>

          <Typography variant="body2" color="text.secondary" sx={{ mt: 2, textAlign: 'center' }}>
            No tienes cuenta? <Link component={RouterLink} to="/auth/jwt/sign-up">Crear organizacion</Link>
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1, textAlign: 'center' }}>
            <Link component={RouterLink} to="/">Volver al inicio</Link>
          </Typography>
        </CardContent>
      </Card>
    </Box>
  );
}
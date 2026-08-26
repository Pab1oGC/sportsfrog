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
import { Iconify } from 'src/components/iconify';
import { apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';

export default function SignUpPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState({ name: '', slug: '', ownerFullName: '', ownerEmail: '', ownerPassword: '' });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const handleSubmit = useCallback(async (e) => {
    e.preventDefault();
    setLoading(true); setError(''); setSuccess('');
    try {
      await apiPost(endpoints.auth.signUp, {
        name: form.name,
        slug: form.slug.toLowerCase().replace(/[^a-z0-9-]/g, ''),
        ownerFullName: form.ownerFullName,
        ownerEmail: form.ownerEmail,
        ownerPassword: form.ownerPassword,
      });
      setSuccess('Organizacion creada! Redirigiendo...');
      setTimeout(() => navigate('/auth/jwt/sign-in'), 2000);
    } catch (err) { setError(err.message || 'Error al registrar'); }
    finally { setLoading(false); }
  }, [form, navigate]);

  const update = (field) => (e) => setForm({ ...form, [field]: e.target.value });

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'grey.100', p: 2 }}>
      <Card sx={{ width: 480, maxWidth: '100%' }}>
        <CardContent sx={{ p: 4 }}>
          <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', mb: 3 }}>
            <Box sx={{ width: 56, height: 56, borderRadius: 2, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center', mb: 2 }}>
              <Typography sx={{ color: 'white', fontWeight: 800, fontSize: 24 }}>SF</Typography>
            </Box>
            <Typography variant="h5" fontWeight={700}>Crear organizacion</Typography>
            <Typography variant="body2" color="text.secondary">Registra tu federacion o liga deportiva</Typography>
          </Box>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          {success && <Alert severity="success" sx={{ mb: 2 }}>{success}</Alert>}
          <form onSubmit={handleSubmit}>
            <TextField fullWidth label="Nombre de la organizacion" value={form.name} onChange={update('name')} sx={{ mb: 2 }} required />
            <TextField fullWidth label="Direccion web (slug)" value={form.slug} onChange={update('slug')} sx={{ mb: 2 }} required helperText="Solo minusculas, numeros y guiones." />
            <TextField fullWidth label="Nombre del responsable" value={form.ownerFullName} onChange={update('ownerFullName')} sx={{ mb: 2 }} required />
            <TextField fullWidth label="Correo electronico" type="email" value={form.ownerEmail} onChange={update('ownerEmail')} sx={{ mb: 2 }} required />
            <TextField fullWidth label="Contrasena" type="password" value={form.ownerPassword} onChange={update('ownerPassword')} sx={{ mb: 3 }} required helperText="Minimo 8 caracteres." />
            <Button fullWidth type="submit" variant="contained" size="large" disabled={loading}
              startIcon={loading ? <CircularProgress size={20} /> : <Iconify icon="eva:person-add-outline" />}>
              Crear organizacion
            </Button>
          </form>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 2, textAlign: 'center' }}>
            Ya tienes cuenta? <Link component={RouterLink} to="/auth/jwt/sign-in">Iniciar sesion</Link>
          </Typography>
        </CardContent>
      </Card>
    </Box>
  );
}
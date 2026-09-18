import { useState } from 'react';
import { useNavigate } from 'react-router';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import InputAdornment from '@mui/material/InputAdornment';
import IconButton from '@mui/material/IconButton';
import Alert from '@mui/material/Alert';
import { toast } from 'sonner';

import { Iconify } from 'src/components/iconify';
import { PageHeader } from 'src/components/page-header';
import { useAuthContext } from 'src/auth/hooks';
import { apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { aSlug, normalizarSlug, problemaDeSlug } from 'src/lib/slug';

const emptyForm = () => ({
  name: '',
  slug: '',
  ownerFullName: '',
  ownerEmail: '',
  ownerPassword: '',
});

export default function RegisterOrganizationPage() {
  const navigate = useNavigate();
  const { user } = useAuthContext();
  const [form, setForm] = useState(emptyForm());
  const [saving, setSaving] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [slugManual, setSlugManual] = useState(false);

  // Solo la cuenta de plataforma (frogtech-solutions) tiene acceso
  const isPlatformAdmin = user?.organizations?.some(
    (org) => org.slug === 'frogtech-solutions'
  );

  if (!isPlatformAdmin) {
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="error">
          No tienes permisos para acceder a esta sección. Solo el administrador de la plataforma puede registrar nuevas organizaciones.
        </Alert>
      </Box>
    );
  }

  const handleNameChange = (e) => {
    const name = e.target.value;
    setForm((prev) => ({
      ...prev,
      name,
      slug: slugManual ? prev.slug : aSlug(name),
    }));
  };

  const handleSlugChange = (e) => {
    setSlugManual(true);
    setForm((prev) => ({
      ...prev,
      slug: normalizarSlug(e.target.value),
    }));
  };

  const slugError = form.slug ? problemaDeSlug(form.slug) : null;

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!form.name.trim()) {
      toast.error('El nombre de la organización es obligatorio.');
      return;
    }

    if (slugError) {
      toast.error(slugError);
      return;
    }

    if (!form.ownerFullName.trim()) {
      toast.error('El nombre del responsable es obligatorio.');
      return;
    }

    if (!form.ownerEmail.trim()) {
      toast.error('El correo electrónico es obligatorio.');
      return;
    }

    if (!form.ownerPassword) {
      toast.error('La contraseña del responsable es obligatoria.');
      return;
    }

    setSaving(true);
    try {
      await apiPost(endpoints.organizations.list, {
        name: form.name.trim(),
        slug: normalizarSlug(form.slug),
        ownerFullName: form.ownerFullName.trim(),
        ownerEmail: form.ownerEmail.trim().toLowerCase(),
        ownerPassword: form.ownerPassword,
      });

      toast.success('Organización registrada exitosamente.');
      setForm(emptyForm());
      setSlugManual(false);
      navigate('/dashboard/organization');
    } catch (err) {
      toast.error(err.message || 'No se pudo registrar la organización.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Crear Nueva Organización"
        subtitle="Registra una nueva organización y asigna las credenciales para su administrador."
      />

      <Card sx={{ maxWidth: 640, mt: 3 }}>
        <CardContent component="form" onSubmit={handleSubmit} sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
          <Typography variant="subtitle1" fontWeight={700} color="primary.main">
            Datos de la Organización
          </Typography>

          <TextField
            label="Nombre de la Organización"
            placeholder="ej. Liga Deportiva Regional"
            value={form.name}
            onChange={handleNameChange}
            required
            fullWidth
          />

          <TextField
            label="Identificador único (Slug)"
            placeholder="ej. liga-deportiva-regional"
            value={form.slug}
            onChange={handleSlugChange}
            error={!!slugError}
            helperText={slugError || `Dirección pública: /public/${form.slug || 'slug'}`}
            required
            fullWidth
          />

          <Typography variant="subtitle1" fontWeight={700} color="primary.main" sx={{ mt: 1 }}>
            Responsable / Dueño de la Organización
          </Typography>

          <TextField
            label="Nombre Completo del Responsable"
            placeholder="ej. Juan Pérez"
            value={form.ownerFullName}
            onChange={(e) => setForm({ ...form, ownerFullName: e.target.value })}
            required
            fullWidth
          />

          <TextField
            label="Correo Electrónico"
            placeholder="ej. admin@organizacion.com"
            type="email"
            value={form.ownerEmail}
            onChange={(e) => setForm({ ...form, ownerEmail: e.target.value })}
            required
            fullWidth
          />

          <TextField
            label="Contraseña Inicial"
            type={showPassword ? 'text' : 'password'}
            value={form.ownerPassword}
            onChange={(e) => setForm({ ...form, ownerPassword: e.target.value })}
            helperText="Mínimo 8 caracteres, al menos una mayúscula, una minúscula, un número y un símbolo especial."
            required
            fullWidth
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton onClick={() => setShowPassword((p) => !p)} edge="end">
                    <Iconify icon={showPassword ? 'solar:eye-bold' : 'solar:eye-closed-bold'} />
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />

          <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1.5, mt: 2 }}>
            <Button
              variant="outlined"
              color="inherit"
              onClick={() => navigate('/dashboard')}
              disabled={saving}
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="contained"
              disabled={saving}
              startIcon={saving ? <CircularProgress size={18} color="inherit" /> : <Iconify icon="mdi:domain-plus" />}
            >
              {saving ? 'Registrando...' : 'Registrar Organización'}
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}

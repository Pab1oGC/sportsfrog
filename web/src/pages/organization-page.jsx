import { useState, useEffect } from 'react';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CircularProgress from '@mui/material/CircularProgress';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { leerComoDataUrl } from 'src/lib/data-url';
import { PageHeader } from 'src/components/page-header';

// Mismo patron de tres estados que el logo de un club: null no toca lo que
// ya hay, '' lo quita, una data URL lo reemplaza.
const emptyForm = () => ({ name: '', logoUrl: null, currentLogo: null });

export default function OrganizationPage() {
  const { data, isLoading, mutate } = useApi(endpoints.organizations.list);
  const [form, setForm] = useState(emptyForm());
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (data) setForm({ name: data.name || '', logoUrl: null, currentLogo: data.logoUrl || null });
  }, [data]);

  const elegirLogo = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    const dataUrl = await leerComoDataUrl(file);
    setForm((f) => ({ ...f, logoUrl: dataUrl }));
  };

  const quitarLogo = () => setForm((f) => ({ ...f, logoUrl: '', currentLogo: null }));

  const guardar = async () => {
    setSaving(true);
    try {
      await apiPut(endpoints.organizations.list, { name: form.name, logoUrl: form.logoUrl });
      mutate();
      toast.success('Organizacion actualizada.');
    } catch (err) { toast.error(err.message || 'No se pudo guardar.'); }
    finally { setSaving(false); }
  };

  const vistaPrevia = form.logoUrl || form.currentLogo;

  if (isLoading && !data) {
    return <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>;
  }

  return (
    <Box>
      <PageHeader title="Organizacion" />
      <Card sx={{ maxWidth: 520 }}>
        <CardContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            label="Nombre"
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
            fullWidth
          />

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            <Avatar src={vistaPrevia || undefined} variant="rounded" sx={{ width: 64, height: 64, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
              {!vistaPrevia && <Iconify icon="mdi:domain" width={32} sx={{ color: 'text.disabled' }} />}
            </Avatar>
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
              <Button variant="outlined" component="label" size="small" startIcon={<Iconify icon="eva:upload-outline" width={16} />}>
                {vistaPrevia ? 'Reemplazar logo' : 'Subir logo'}
                <input type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={elegirLogo} />
              </Button>
              {vistaPrevia && <Button size="small" color="inherit" onClick={quitarLogo}>Quitar</Button>}
            </Box>
          </Box>
          <Typography variant="caption" color="text.secondary" sx={{ mt: -1 }}>
            PNG, JPEG o WebP. Se muestra en el encabezado del portal publico de cada competencia.
          </Typography>

          <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 1 }}>
            <Button variant="contained" onClick={guardar} disabled={saving}>
              {saving ? <CircularProgress size={20} sx={{ mr: 1 }} /> : null}
              Guardar
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}

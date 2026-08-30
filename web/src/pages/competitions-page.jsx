import { useState, useCallback } from 'react';
import { useNavigate } from 'react-router';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Alert from '@mui/material/Alert';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import Accordion from '@mui/material/Accordion';
import AccordionSummary from '@mui/material/AccordionSummary';
import AccordionDetails from '@mui/material/AccordionDetails';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import Divider from '@mui/material/Divider';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { leerComoDataUrl } from 'src/lib/data-url';
import { aSlug, normalizarSlug, problemaDeSlug, slugDeOrganizacion, SLUG_MAX } from 'src/lib/slug';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { useConfirm } from 'src/components/confirm-dialog';
import { toast } from 'sonner';

const SC = { draft: 'default', scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error' };
const SL = { draft: 'Borrador', scheduled: 'Programada', in_progress: 'En curso', finished: 'Finalizada', cancelled: 'Cancelada' };
const FORMATS = ['league', 'knockout', 'groups'];
const CAPS = ['basic', 'detailed'];
const NEXT_STATUS = { draft: ['scheduled', 'cancelled'], scheduled: ['draft', 'in_progress', 'cancelled'], in_progress: ['finished'], finished: [], cancelled: [] };
const FORMATO_INFO = {
  league: 'Todos juegan contra todos.',
  knockout: 'El que pierde queda afuera.',
  groups: 'Primero zonas, despues llaves.',
};

// La personalizacion del portal viaja aparte de los campos de siempre porque
// no comparte su forma: son las mismas claves que guarda el backend en
// settings.public, mas currentBannerUrl/currentLogoUrl, que son solo vista
// previa y nunca se envian. schedule pasa de largo, sin editarse aca — se
// guarda tal cual llego, para no perder la disponibilidad configurada desde
// otra pantalla al guardar este formulario (settings se reemplaza entero).
const emptyForm = () => ({
  name: '', slug: '', season: '', format: 'league', rulesetId: '', captureLevel: 'basic',
  schedule: null,
  showStandings: true, showLeaders: true, showRosters: false,
  bannerKey: null, currentBannerUrl: null,
  accentColor: '', description: '',
  instagram: '', facebook: '', whatsApp: '', website: '',
  sponsors: [],
});

/** La imagen a mostrar para una clave de almacenamiento: la data URL recien
 * elegida si se acaba de subir, o el enlace firmado que ya se leyo, si no. */
function vistaPreviaImagen(key, currentUrl) {
  if (!key) return null;
  return key.startsWith('data:') ? key : currentUrl;
}

export default function CompetitionsPage() {
  const confirm = useConfirm();
  const navigate = useNavigate();
  const { data, mutate, isLoading } = useApi(endpoints.competitions);
  const { data: rulesets } = useApi(endpoints.rulesets);

  const [open, setOpen] = useState(false);
  const [editId, setEditId] = useState(null);
  const [form, setForm] = useState(emptyForm());
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [slugTouched, setSlugTouched] = useState(false);

  const slugError = form.slug || slugTouched ? problemaDeSlug(form.slug) : null;
  const slugHelp = `/${slugDeOrganizacion()}/${normalizarSlug(form.slug) || '...'}`;

  const openDialog = useCallback((row) => {
    setError(null);
    if (row) {
      setEditId(row.id); setSlugTouched(true);
      const pub = (row.settings && row.settings.public) || {};
      const preview = row.publicPreview || {};
      const previewByKey = {};
      (preview.sponsors || []).forEach((s) => { previewByKey[s.logoKey] = s.logoUrl; });
      setForm({
        name: row.name, slug: row.slug, season: row.season, format: row.format,
        rulesetId: row.rulesetId || '', captureLevel: row.captureLevel,
        schedule: (row.settings && row.settings.schedule) || null,
        showStandings: pub.showStandings !== false,
        showLeaders: pub.showLeaders !== false,
        showRosters: !!pub.showRosters,
        bannerKey: pub.bannerKey || null,
        currentBannerUrl: preview.bannerUrl || null,
        accentColor: pub.accentColor || '',
        description: pub.description || '',
        instagram: pub.instagram || '', facebook: pub.facebook || '',
        whatsApp: pub.whatsApp || '', website: pub.website || '',
        sponsors: (pub.sponsors || []).map((s) => ({
          logoKey: s.logoKey, name: s.name || '', url: s.url || '',
          currentLogoUrl: previewByKey[s.logoKey] || null,
        })),
      });
    } else {
      setEditId(null); setSlugTouched(false); setForm(emptyForm());
    }
    setOpen(true);
  }, []);

  const elegirBanner = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    const dataUrl = await leerComoDataUrl(file);
    setForm((f) => ({ ...f, bannerKey: dataUrl }));
  };

  const quitarBanner = () => setForm((f) => ({ ...f, bannerKey: null, currentBannerUrl: null }));

  const agregarAuspiciante = () => setForm((f) => ({
    ...f, sponsors: [...f.sponsors, { logoKey: '', name: '', url: '', currentLogoUrl: null }],
  }));

  const quitarAuspiciante = (i) => setForm((f) => ({ ...f, sponsors: f.sponsors.filter((_, idx) => idx !== i) }));

  const editarAuspiciante = (i, patch) => setForm((f) => ({
    ...f, sponsors: f.sponsors.map((s, idx) => (idx === i ? { ...s, ...patch } : s)),
  }));

  const elegirLogoAuspiciante = async (i, e) => {
    const file = e.target.files[0];
    if (!file) return;
    const dataUrl = await leerComoDataUrl(file);
    editarAuspiciante(i, { logoKey: dataUrl, currentLogoUrl: null });
  };

  const handleNameChange = (e) => {
    const name = e.target.value;
    setForm((f) => ({ ...f, name, slug: slugTouched ? f.slug : aSlug(name) }));
  };

  const handleSlugChange = (e) => {
    setSlugTouched(true);
    setForm((f) => ({ ...f, slug: normalizarSlug(e.target.value) }));
  };

  const save = async () => {
    const problem = problemaDeSlug(form.slug);
    if (problem) { setSlugTouched(true); setError(problem); return; }
    setSaving(true); setError('');
    try {
      const body = {
        name: form.name, slug: normalizarSlug(form.slug), season: form.season,
        format: form.format, rulesetId: form.rulesetId, captureLevel: form.captureLevel,
        settings: {
          schedule: form.schedule,
          public: {
            showStandings: form.showStandings,
            showLeaders: form.showLeaders,
            showRosters: form.showRosters,
            bannerKey: form.bannerKey || null,
            accentColor: form.accentColor || null,
            description: form.description || null,
            instagram: form.instagram || null,
            facebook: form.facebook || null,
            whatsApp: form.whatsApp || null,
            website: form.website || null,
            sponsors: form.sponsors.length
              ? form.sponsors.map((s) => ({ logoKey: s.logoKey, name: s.name || null, url: s.url || null }))
              : null,
          },
        },
      };
      if (editId) {
        await apiPut(endpoints.competition(editId), body);
        setOpen(false); mutate(); toast.success('Competencia guardada.');
      } else {
        const created = await apiPost(endpoints.competitions, body);
        setOpen(false); mutate();
        toast.success('Competencia creada. Ahora agregale al menos una categoria.');
        // Sin categorias no hay nada que sortear ni programar (RF backend),
        // asi que el siguiente paso siempre es este — llevar directo ahi en
        // vez de devolver a una lista donde hay que volver a elegirla.
        navigate(`/dashboard/categories?competition=${created.id}`);
      }
    } catch (err) { setError(err.message || 'Error al guardar.'); }
    finally { setSaving(false); }
  };

  const remove = async (id) => {
    const ok = await confirm('Eliminar competicion?', { confirmLabel: 'Eliminar', danger: true });
    if (!ok) return;
    await apiDelete(endpoints.competition(id)); mutate(); toast.success('Competencia eliminada.');
  };

  const changeStatus = async (id, st) => {
    const ok = await confirm(`Cambiar estado a "${SL[st] || st}"?`, { confirmLabel: 'Cambiar' });
    if (!ok) return;
    try { await apiPut(endpoints.competitionStatus(id), { status: st }); mutate(); }
    catch (err) { toast.error(err.message); }
  };

  const togglePublish = async (id, current) => {
    try { await apiPut(endpoints.competitionPublication(id), { isPublic: !current }); mutate(); }
    catch (err) { toast.error(err.message); }
  };

  const schedule = async (id) => {
    const ok = await confirm('Programar calendario automaticamente?', { confirmLabel: 'Programar' });
    if (!ok) return;
    try { const r = await apiPost(endpoints.competitionSchedule(id), {}); mutate(); toast.success(`Colocados: ${r.placed}, Sin lugar: ${r.unplaced}`); }
    catch (err) { toast.error(err.message); }
  };

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 180 },
    { field: 'sportCode', headerName: 'Deporte', width: 100 },
    { field: 'season', headerName: 'Temporada', width: 120 },
    { field: 'format', headerName: 'Formato', width: 110, renderCell: ({ value }) => value === 'league' ? 'Todos vs todos' : value === 'knockout' ? 'Eliminacion' : 'Grupos' },
    { field: 'status', headerName: 'Estado', width: 190, renderCell: ({ value, row }) => (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
        <Chip label={SL[value] || value} color={SC[value] || 'default'} size="small" />
        {row.categoryCount === 0 && (
          <Tooltip title="Sin categorias todavia: no se puede sortear ni programar. Agregale una desde Categorias.">
            <Chip
              label="Sin categorias"
              color="warning"
              variant="outlined"
              size="small"
              icon={<Iconify icon="eva:alert-triangle-outline" width={14} />}
              onClick={() => navigate(`/dashboard/categories?competition=${row.id}`)}
              sx={{ cursor: 'pointer' }}
            />
          </Tooltip>
        )}
      </Box>
    )},
    { field: 'isPublic', headerName: 'Publica', width: 80, renderCell: ({ value, row }) => (
      <Tooltip title="Alternar publicacion">
        <IconButton size="small" onClick={() => togglePublish(row.id, value)}>
          <Chip label={value ? 'Si' : 'No'} color={value ? 'success' : 'default'} size="small" variant="outlined" icon={<Iconify icon={value ? 'eva:globe-fill' : 'eva:eye-off-outline'} width={14} />} />
        </IconButton>
      </Tooltip>
    )},
    { field: 'actions', headerName: '', width: 220, renderCell: ({ row }) => {
      const next = NEXT_STATUS[row.status] || [];
      return (
        <Box sx={{ display: 'flex' }}>
          {next.map((s) => {
            const icons = { scheduled: 'eva:calendar-outline', in_progress: 'eva:play-circle-fill', finished: 'eva:checkmark-circle-fill', draft: 'eva:edit-fill', cancelled: 'eva:close-circle-fill' };
            const colors = { scheduled: 'info', in_progress: 'warning', finished: 'success', draft: 'default', cancelled: 'error' };
            return <Tooltip key={s} title={SL[s]}><IconButton size="small" onClick={() => changeStatus(row.id, s)}><Iconify icon={icons[s] || 'eva:arrow-right-fill'} width={18} sx={{ color: `${colors[s]}.main` }} /></IconButton></Tooltip>;
          })}
          {row.status === 'scheduled' && <Tooltip title="Programar"><IconButton size="small" onClick={() => schedule(row.id)}><Iconify icon="eva:clock-outline" width={18} sx={{ color: 'info.main' }} /></IconButton></Tooltip>}
          <Tooltip title="Editar"><IconButton size="small" onClick={() => openDialog(row)}><Iconify icon="eva:edit-fill" width={18} /></IconButton></Tooltip>
          <Tooltip title="Eliminar"><IconButton size="small" onClick={() => remove(row.id)}><Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} /></IconButton></Tooltip>
        </Box>
      );
    }},
  ];

  return (
    <Box>
      <PageHeader title="Competiciones" actionLabel="Nueva" onAction={() => openDialog(null)} />
      <DataGrid rows={data || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />
      <CrudDialog open={open} editId={editId} entityName="Competicion" error={error} saving={saving} onClose={() => setOpen(false)} onSave={save} maxWidth="md">
        <TextField label="Nombre" value={form.name} onChange={handleNameChange} fullWidth required />
        <TextField label="Direccion publica (slug)" value={form.slug} onChange={handleSlugChange} fullWidth required error={!!slugError} helperText={slugError || slugHelp}
          slotProps={{ htmlInput: { maxLength: SLUG_MAX, spellCheck: false, autoCapitalize: 'none' } }} />
        {editId && <Alert severity="warning" sx={{ mt: -1 }}>Cambiar la direccion rompe enlaces existentes.</Alert>}
        <TextField label="Temporada" value={form.season} onChange={(e) => setForm({ ...form, season: e.target.value })} fullWidth required />
        <TextField select label="Formato" value={form.format} onChange={(e) => setForm({ ...form, format: e.target.value })} fullWidth helperText={FORMATO_INFO[form.format]}>
          {FORMATS.map((f) => <MenuItem key={f} value={f}>{f === 'league' ? 'Todos vs todos' : f === 'knockout' ? 'Eliminacion' : 'Grupos'}</MenuItem>)}
        </TextField>
        <TextField select label="Reglamento" value={form.rulesetId} onChange={(e) => setForm({ ...form, rulesetId: e.target.value })} fullWidth required>
          <MenuItem value="">Seleccionar</MenuItem>
          {(rulesets || []).map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
        </TextField>
        <TextField select label="Captura" value={form.captureLevel} onChange={(e) => setForm({ ...form, captureLevel: e.target.value })} fullWidth>
          {CAPS.map((c) => <MenuItem key={c} value={c}>{c === 'basic' ? 'Basico' : 'Detallado'}</MenuItem>)}
        </TextField>

        <Accordion disableGutters variant="outlined" sx={{ '&:before': { display: 'none' } }}>
          <AccordionSummary expandIcon={<Iconify icon="eva:chevron-down-fill" />}>
            <Typography variant="subtitle2">Personalizar portal publico</Typography>
          </AccordionSummary>
          <AccordionDetails sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
              <FormControlLabel control={<Switch checked={form.showStandings} onChange={(e) => setForm({ ...form, showStandings: e.target.checked })} />} label="Tabla de posiciones" />
              <FormControlLabel control={<Switch checked={form.showLeaders} onChange={(e) => setForm({ ...form, showLeaders: e.target.checked })} />} label="Lideres" />
              <FormControlLabel control={<Switch checked={form.showRosters} onChange={(e) => setForm({ ...form, showRosters: e.target.checked })} />} label="Nominas" />
            </Stack>

            <Divider />

            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Portada</Typography>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                <Avatar src={vistaPreviaImagen(form.bannerKey, form.currentBannerUrl) || undefined} variant="rounded" sx={{ width: 96, height: 56, bgcolor: 'action.hover' }}>
                  {!vistaPreviaImagen(form.bannerKey, form.currentBannerUrl) && <Iconify icon="mdi:image-outline" width={24} sx={{ color: 'text.disabled' }} />}
                </Avatar>
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
                  <Button variant="outlined" component="label" size="small" startIcon={<Iconify icon="eva:upload-outline" width={16} />}>
                    {form.bannerKey ? 'Reemplazar' : 'Subir portada'}
                    <input type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={elegirBanner} />
                  </Button>
                  {form.bannerKey && <Button size="small" color="inherit" onClick={quitarBanner}>Quitar</Button>}
                </Box>
              </Box>
            </Box>

            <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
              <TextField
                label="Color de acento"
                type="color"
                value={form.accentColor || '#1976d2'}
                onChange={(e) => setForm({ ...form, accentColor: e.target.value })}
                sx={{ width: 140 }}
                helperText={form.accentColor ? null : 'Sin elegir: color por defecto.'}
              />
              {form.accentColor && <Button size="small" color="inherit" onClick={() => setForm({ ...form, accentColor: '' })}>Quitar color</Button>}
            </Box>

            <TextField
              label="Presentacion"
              value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
              fullWidth multiline minRows={2}
              slotProps={{ htmlInput: { maxLength: 500 } }}
              helperText={`${form.description.length}/500`}
            />

            <Divider />
            <Typography variant="caption" color="text.secondary">Redes y contacto</Typography>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <TextField label="Instagram" value={form.instagram} onChange={(e) => setForm({ ...form, instagram: e.target.value })} fullWidth placeholder="https://instagram.com/..." />
              <TextField label="Facebook" value={form.facebook} onChange={(e) => setForm({ ...form, facebook: e.target.value })} fullWidth placeholder="https://facebook.com/..." />
            </Stack>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <TextField label="WhatsApp" value={form.whatsApp} onChange={(e) => setForm({ ...form, whatsApp: e.target.value })} fullWidth placeholder="https://wa.me/59171234567" />
              <TextField label="Sitio web" value={form.website} onChange={(e) => setForm({ ...form, website: e.target.value })} fullWidth placeholder="https://..." />
            </Stack>

            <Divider />
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Typography variant="caption" color="text.secondary">Auspiciantes</Typography>
              <Button size="small" startIcon={<Iconify icon="eva:plus-fill" width={16} />} onClick={agregarAuspiciante}>Agregar</Button>
            </Box>
            {form.sponsors.map((s, i) => (
              <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 1.5, p: 1, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
                <Avatar src={vistaPreviaImagen(s.logoKey, s.currentLogoUrl) || undefined} variant="rounded" sx={{ width: 40, height: 40, bgcolor: 'action.hover', flexShrink: 0, '& img': { objectFit: 'contain' } }}>
                  {!vistaPreviaImagen(s.logoKey, s.currentLogoUrl) && <Iconify icon="mdi:image-outline" width={20} sx={{ color: 'text.disabled' }} />}
                </Avatar>
                <Button variant="outlined" component="label" size="small" sx={{ flexShrink: 0 }}>
                  Logo
                  <input type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={(e) => elegirLogoAuspiciante(i, e)} />
                </Button>
                <TextField label="Nombre" size="small" value={s.name} onChange={(e) => editarAuspiciante(i, { name: e.target.value })} sx={{ flex: 1, minWidth: 100 }} />
                <TextField label="Enlace" size="small" value={s.url} onChange={(e) => editarAuspiciante(i, { url: e.target.value })} sx={{ flex: 1, minWidth: 100 }} />
                <IconButton size="small" onClick={() => quitarAuspiciante(i)}><Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} /></IconButton>
              </Box>
            ))}
          </AccordionDetails>
        </Accordion>
      </CrudDialog>
    </Box>
  );
}
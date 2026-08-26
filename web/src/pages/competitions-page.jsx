import { useState, useCallback } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Alert from '@mui/material/Alert';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { aSlug, normalizarSlug, problemaDeSlug, slugDeOrganizacion, SLUG_MAX } from 'src/lib/slug';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
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

const emptyForm = () => ({ name: '', slug: '', season: '', format: 'league', rulesetId: '', captureLevel: 'basic' });

export default function CompetitionsPage() {
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
      setForm({ name: row.name, slug: row.slug, season: row.season, format: row.format, rulesetId: row.rulesetId || '', captureLevel: row.captureLevel });
    } else {
      setEditId(null); setSlugTouched(false); setForm(emptyForm());
    }
    setOpen(true);
  }, []);

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
      const body = { ...form, slug: normalizarSlug(form.slug) };
      const url = editId ? endpoints.competition(editId) : endpoints.competitions;
      editId ? await apiPut(url, body) : await apiPost(url, body);
      setOpen(false); mutate(); toast.success('Competencia guardada.');
    } catch (err) { setError(err.message || 'Error al guardar.'); }
    finally { setSaving(false); }
  };

  const remove = async (id) => {
    if (!confirm('Eliminar competicion?')) return;
    await apiDelete(endpoints.competition(id)); mutate(); toast.success('Competencia eliminada.');
  };

  const changeStatus = async (id, st) => {
    if (!confirm(`Cambiar estado a "${SL[st] || st}"?`)) return;
    try { await apiPut(endpoints.competitionStatus(id), { status: st }); mutate(); }
    catch (err) { toast.error(err.message); }
  };

  const togglePublish = async (id, current) => {
    try { await apiPut(endpoints.competitionPublication(id), { isPublic: !current }); mutate(); }
    catch (err) { toast.error(err.message); }
  };

  const schedule = async (id) => {
    if (!confirm('Programar calendario automaticamente?')) return;
    try { const r = await apiPost(endpoints.competitionSchedule(id), {}); mutate(); toast.success(`Colocados: ${r.placed}, Sin lugar: ${r.unplaced}`); }
    catch (err) { toast.error(err.message); }
  };

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 180 },
    { field: 'sportCode', headerName: 'Deporte', width: 100 },
    { field: 'season', headerName: 'Temporada', width: 120 },
    { field: 'format', headerName: 'Formato', width: 110, renderCell: ({ value }) => value === 'league' ? 'Todos vs todos' : value === 'knockout' ? 'Eliminacion' : 'Grupos' },
    { field: 'status', headerName: 'Estado', width: 130, renderCell: ({ value }) => <Chip label={SL[value] || value} color={SC[value] || 'default'} size="small" /> },
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
            const icons = { scheduled: 'eva:calendar-outline', in_progress: 'eva:play-fill', finished: 'eva:checkmark-circle-fill', draft: 'eva:edit-fill', cancelled: 'eva:close-circle-fill' };
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
      <CrudDialog open={open} editId={editId} entityName="Competicion" error={error} saving={saving} onClose={() => setOpen(false)} onSave={save}>
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
      </CrudDialog>
    </Box>
  );
}
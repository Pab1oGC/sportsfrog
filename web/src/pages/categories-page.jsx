import { useState } from 'react';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { SelectionCompetition, SelectionField } from 'src/components/selectors';
import { toast } from 'sonner';

const emptyForm = () => ({ name: '', gender: '', birthDateFrom: '', birthDateTo: '', maxRosterSize: '', displayOrder: 0, rulesetId: '' });

export default function CategoriesPage() {
  const [compId, setCompId] = useState('');
  const { data: comps } = useApi(endpoints.competitions);
  const { data, mutate, isLoading } = useApi(compId ? endpoints.categories(compId) : null);
  const { data: rulesets } = useApi(endpoints.rulesets);
  const { data: sports } = useApi(endpoints.sports);

  const [open, setOpen] = useState(false);
  const [editId, setEditId] = useState(null);
  const [form, setForm] = useState(emptyForm());
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);

  const comp = comps?.find((c) => c.id === compId);
  const possibleRulesets = (rulesets || []).filter((r) => comp && r.sportCode === comp.sportCode);
  const sport = sports?.find((s) => comp && s.code === comp.sportCode);
  const sportName = sport?.name || comp?.sportCode || '';

  const openDialog = (row) => {
    setError(null);
    setEditId(row?.id || null);
    setForm(row ? { name: row.name || '', gender: row.gender || '', birthDateFrom: row.birthDateFrom || '', birthDateTo: row.birthDateTo || '', maxRosterSize: row.maxRosterSize || '', displayOrder: row.displayOrder || 0, rulesetId: row.rulesetId || '' } : emptyForm());
    setOpen(true);
  };

  const save = async () => {
    if (!compId) return; setSaving(true); setError(null);
    try {
      const body = { name: form.name, gender: form.gender || null, birthDateFrom: form.birthDateFrom || null, birthDateTo: form.birthDateTo || null, maxRosterSize: form.maxRosterSize ? Number(form.maxRosterSize) : null, displayOrder: Number(form.displayOrder), rulesetId: form.rulesetId || null };
      editId ? await apiPut(endpoints.category(compId, editId), body) : await apiPost(endpoints.categories(compId), body);
      setOpen(false); mutate(); toast.success('Categoria guardada.');
    } catch (err) { setError(err.message); }
    finally { setSaving(false); }
  };

  const remove = async (catId) => {
    if (!compId || !confirm('Eliminar categoria?')) return;
    await apiDelete(endpoints.category(compId, catId)); mutate(); toast.success('Categoria eliminada.');
  };

  const columns = [
    { field: 'displayOrder', headerName: '#', width: 60 },
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 180 },
    { field: 'gender', headerName: 'Genero', width: 90, renderCell: ({ value }) => value === 'M' ? 'Masculino' : value === 'F' ? 'Femenino' : 'Abierto' },
    { field: 'birthDateFrom', headerName: 'Nac. desde', width: 120 },
    { field: 'birthDateTo', headerName: 'Nac. hasta', width: 120 },
    { field: 'maxRosterSize', headerName: 'Max nomina', width: 100, renderCell: ({ value }) => value || '--' },
    { field: 'actions', headerName: '', width: 100, renderCell: ({ row }) => (
      <div style={{ display: 'flex', gap: 4 }}>
        <Iconify icon="eva:edit-fill" sx={{ cursor: 'pointer', color: 'text.secondary' }} onClick={() => openDialog(row)} />
        <Iconify icon="eva:trash-2-outline" sx={{ cursor: 'pointer', color: 'error.main' }} onClick={() => remove(row.id)} />
      </div>
    )},
  ];

  return (
    <div>
      <PageHeader title="Categorias" actionLabel="Nueva categoria" onAction={() => openDialog(null)}>
        <Box sx={{ width: 300 }}><SelectionCompetition value={compId} onChange={(e) => setCompId(e.target.value)} required /></Box>
      </PageHeader>
      <DataGrid rows={data || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />
      <CrudDialog open={open} editId={editId} entityName="Categoria" error={error} saving={saving} onClose={() => setOpen(false)} onSave={save}>
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth required />
        <SelectionField label="Genero" value={form.gender} onChange={(e) => setForm({ ...form, gender: e.target.value })} emptyLabel="Abierto" options={[{ value: 'M', label: 'Masculino' }, { value: 'F', label: 'Femenino' }]} />
        <TextField label="Nac. desde" type="date" value={form.birthDateFrom} onChange={(e) => setForm({ ...form, birthDateFrom: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
        <TextField label="Nac. hasta" type="date" value={form.birthDateTo} onChange={(e) => setForm({ ...form, birthDateTo: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
        <TextField label="Max. nomina" type="number" value={form.maxRosterSize} onChange={(e) => setForm({ ...form, maxRosterSize: e.target.value })} fullWidth />
        <TextField label="Orden" type="number" value={form.displayOrder} onChange={(e) => setForm({ ...form, displayOrder: e.target.value })} fullWidth />
        <TextField select label="Reglamento propio" value={form.rulesetId} onChange={(e) => setForm({ ...form, rulesetId: e.target.value })} fullWidth disabled={possibleRulesets.length === 0}>
          <MenuItem value="">Usar el de la competencia</MenuItem>
          {possibleRulesets.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
        </TextField>
      </CrudDialog>
    </div>
  );
}
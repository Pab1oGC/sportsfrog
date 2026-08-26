import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import TextField from '@mui/material/TextField';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { SelectionCompetition, SelectionCategory, SelectionClub } from 'src/components/selectors';
import { toast } from 'sonner';

export default function TeamsPage() {
  const cascade = useCascade();
  const { data: teams, mutate, isLoading } = useApi(cascade.catId ? endpoints.teams(cascade.catId) : null);

  const [open, setOpen] = React.useState(false);
  const [editId, setEditId] = React.useState(null);
  const [form, setForm] = React.useState({ clubId: '', name: '', groupLabel: '' });
  const [error, setError] = React.useState('');

  const openRow = (row) => {
    setEditId(row?.id || null);
    setForm(row ? { clubId: row.clubId || '', name: row.name || '', groupLabel: row.groupLabel || '' } : { clubId: '', name: '', groupLabel: '' });
    setError(''); setOpen(true);
  };

  const save = async () => {
    if (!cascade.catId) return; setError('');
    try {
      const body = { clubId: form.clubId, name: form.name || null, groupLabel: form.groupLabel || null };
      editId ? await apiPut(endpoints.team(editId), body) : await apiPost(endpoints.teams(cascade.catId), body);
      setOpen(false); mutate(); toast.success('Equipo guardado.');
    } catch (err) { setError(err.message); }
  };

  const remove = async (id) => {
    if (!confirm('Eliminar equipo?')) return;
    await apiDelete(endpoints.team(id)); mutate(); toast.success('Equipo eliminado.');
  };

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'clubName', headerName: 'Club', width: 160 },
    { field: 'groupLabel', headerName: 'Grupo', width: 100, renderCell: ({ value }) => value ? <Chip label={value} size="small" /> : '--' },
    { field: 'isActive', headerName: 'Activo', width: 80, renderCell: ({ value }) => <Chip label={value ? 'Si' : 'No'} color={value ? 'success' : 'default'} size="small" variant="outlined" /> },
    { field: 'actions', headerName: '', width: 100, renderCell: ({ row }) => (
      <Box sx={{ display: 'flex', gap: 0.5 }}>
        <Iconify icon="eva:edit-fill" sx={{ cursor: 'pointer', color: 'text.secondary' }} onClick={() => openRow(row)} />
        <Iconify icon="eva:trash-2-outline" sx={{ cursor: 'pointer', color: 'error.main' }} onClick={() => remove(row.id)} />
      </Box>
    )},
  ];

  return (
    <Box>
      <PageHeader title="Equipos" actionLabel="Nuevo equipo" onAction={() => openRow(null)} actionDisabled={!cascade.catId} />
      <CascadeFilters cascade={cascade} />
      <DataGrid rows={teams || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />
      <CrudDialog open={open} editId={editId} entityName="Equipo" error={error} onClose={() => setOpen(false)} onSave={save}>
        <SelectionClub value={form.clubId} onChange={(e) => setForm({ ...form, clubId: e.target.value })} required />
        <TextField label="Nombre del equipo (vacio = nombre del club)" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth helperText="Dejar vacio para usar el nombre del club" />
        <TextField label="Grupo / Zona" value={form.groupLabel} onChange={(e) => setForm({ ...form, groupLabel: e.target.value })} fullWidth helperText="Para formato de grupos: A, B, etc." />
      </CrudDialog>
    </Box>
  );
}

function CascadeFilters({ cascade }) {
  return (
    <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700 }}>
      <Box sx={{ flex: 1 }}>
        <SelectionCompetition value={cascade.compId} onChange={(e) => cascade.setCompId(e.target.value)} required />
      </Box>
      <Box sx={{ flex: 1 }}>
        <SelectionCategory competitionId={cascade.compId} value={cascade.catId} onChange={(e) => cascade.setCatId(e.target.value)} required />
      </Box>
    </Box>
  );
}
import { useState } from 'react';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import FormControlLabel from '@mui/material/FormControlLabel';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { SelectionCompetition, SelectionCategory, SelectionClub } from 'src/components/selectors';
import { useConfirm } from 'src/components/confirm-dialog';
import { toast } from 'sonner';

export default function TeamsPage() {
  const confirm = useConfirm();
  const cascade = useCascade();
  const { data: teams, mutate, isLoading } = useApi(cascade.catId ? endpoints.teams(cascade.catId) : null);

  const [open, setOpen] = useState(false);
  const [editId, setEditId] = useState(null);
  const [form, setForm] = useState({ clubId: '', name: '', groupLabel: '', seed: '', isActive: true });
  const [error, setError] = useState('');

  const openRow = (row) => {
    setEditId(row?.id || null);
    setForm(row
      ? { clubId: row.clubId || '', name: row.name || '', groupLabel: row.groupLabel || '', seed: row.seed ?? '', isActive: row.isActive !== false }
      : { clubId: '', name: '', groupLabel: '', seed: '', isActive: true });
    setError(''); setOpen(true);
  };

  const save = async () => {
    if (!cascade.catId) return; setError('');
    try {
      // isActive solo lo pide UpdateTeam (al crear siempre arranca activo),
      // pero mandarlo tambien en la creacion no molesta.
      const body = { clubId: form.clubId, name: form.name || null, groupLabel: form.groupLabel || null, seed: form.seed !== '' ? Number(form.seed) : null, isActive: form.isActive };
      editId ? await apiPut(endpoints.team(editId), body) : await apiPost(endpoints.teams(cascade.catId), body);
      setOpen(false); mutate(); toast.success('Equipo guardado.');
    } catch (err) { setError(err.message); }
  };

  const remove = async (id) => {
    const ok = await confirm('Eliminar equipo?', { confirmLabel: 'Eliminar', danger: true });
    if (!ok) return;
    await apiDelete(endpoints.team(id)); mutate(); toast.success('Equipo eliminado.');
  };

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'clubName', headerName: 'Club', width: 160 },
    { field: 'groupLabel', headerName: 'Grupo', width: 100, renderCell: ({ value }) => value ? <Chip label={value} size="small" /> : '--' },
    { field: 'seed', headerName: 'Bombo', width: 90, renderCell: ({ value }) => value != null ? value : '--' },
    { field: 'isActive', headerName: 'Activo', width: 80, renderCell: ({ value }) => <Chip label={value ? 'Si' : 'No'} color={value ? 'success' : 'default'} size="small" variant="outlined" /> },
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openRow(row)} onDelete={() => remove(row.id)} />
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
        {/* El grupo ya no se tipea a mano: lo completa el sorteo de grupos
            (ver Fixtures/Partidos). El campo seguia en el formulario aunque
            eso ya lo resolvia solo — se saca de aca, pero el valor que ya
            tenga un equipo (asignado por un sorteo anterior) no se toca:
            form.groupLabel sigue viajando en el guardado, solo que ya no hay
            forma de escribirlo a mano. */}
        <TextField
          label="Bombo"
          type="number"
          value={form.seed}
          onChange={(e) => setForm({ ...form, seed: e.target.value })}
          fullWidth
          helperText="Opcional. Para un sorteo de grupos por bombos: equipos del mismo bombo nunca caen en el mismo grupo."
          slotProps={{ htmlInput: { min: 1, max: 26 } }}
        />
        {editId && <FormControlLabel control={<Switch checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />} label="Activo" />}
      </CrudDialog>
    </Box>
  );
}

function CascadeFilters({ cascade }) {
  return (
    <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
      <Box sx={{ flex: 1, minWidth: 200 }}>
        <SelectionCompetition value={cascade.compId} onChange={(e) => cascade.setCompId(e.target.value)} required />
      </Box>
      <Box sx={{ flex: 1, minWidth: 200 }}>
        <SelectionCategory competitionId={cascade.compId} value={cascade.catId} onChange={(e) => cascade.setCatId(e.target.value)} required />
      </Box>
    </Box>
  );
}
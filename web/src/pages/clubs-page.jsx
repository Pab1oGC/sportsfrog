import Switch from '@mui/material/Switch';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useCrudDialog } from 'src/hooks/use-crud';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';

export default function ClubsPage() {
  const { rows, isLoading, open, editId, form, setForm, error, saving, openCreate, openEdit, close, save, remove } = useCrudDialog({
    resourceUrl: '/api/clubs',
    emptyForm: { name: '', shortName: '' },
    entityName: 'club',
  });

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'shortName', headerName: 'Abrev.', width: 120 },
    { field: 'isActive', headerName: 'Activo', width: 80, renderCell: ({ value }) => <Switch checked={value} disabled size="small" /> },
    { field: 'actions', headerName: '', width: 100, renderCell: ({ row }) => (
      <Box sx={{ display: 'flex', gap: 0.5 }}>
        <Iconify icon="eva:edit-fill" sx={{ cursor: 'pointer', color: 'text.secondary' }} onClick={() => openEdit(row)} />
        <Iconify icon="eva:trash-2-outline" sx={{ cursor: 'pointer', color: 'error.main' }} onClick={() => remove(row.id)} />
      </Box>
    )},
  ];

  return (
    <Box>
      <PageHeader title="Clubes" actionLabel="Nuevo club" onAction={openCreate} />
      <DataGrid rows={rows} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick />
      <CrudDialog open={open} editId={editId} entityName="Club" error={error} saving={saving} onClose={close} onSave={save}>
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth />
        <TextField label="Abreviatura" value={form.shortName} onChange={(e) => setForm({ ...form, shortName: e.target.value })} fullWidth />
      </CrudDialog>
    </Box>
  );
}
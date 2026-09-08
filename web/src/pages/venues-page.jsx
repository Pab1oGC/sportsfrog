import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Alert from '@mui/material/Alert';
import TextField from '@mui/material/TextField';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { EditDeleteActions } from 'src/components/edit-delete-actions';

const emptyVenueForm = () => ({ name: '', address: '', isActive: true });
const emptySpaceForm = () => ({ name: '', isActive: true });

export default function VenuesPage() {
  // Venues
  const {
    rows: venues, isLoading: lVenues, open: vOpen, editId: vEditId, form: vForm, setForm: setVForm,
    openCreate: openCreateVenue, openEdit: openEditVenue, close: closeVenue, save: saveVenue, remove: removeVenue,
  } = useCrudDialog({
    resourceUrl: endpoints.venues,
    emptyForm: emptyVenueForm,
    entityName: 'sede',
    entityGender: 'f',
    buildUrl: (base, id) => endpoints.venue(id),
    mapToForm: (row) => ({ name: row.name, address: row.address || '', isActive: row.isActive !== false }),
  });

  // Spaces (depend on selected venue)
  const [selected, setSelected] = useState(null);
  const {
    rows: spaces, isLoading: lSpaces, open: sOpen, editId: sEditId, form: sForm, setForm: setSForm, error: sErr,
    openCreate: openCreateSpace, openEdit: openEditSpace, close: closeSpace, save: saveSpace, remove: removeSpace,
  } = useCrudDialog({
    resourceUrl: selected ? endpoints.venueSpaces(selected.id) : null,
    emptyForm: emptySpaceForm,
    entityName: 'espacio',
    buildUrl: (base, id) => endpoints.space(id),
    mapToForm: (row) => ({ name: row.name, isActive: row.isActive }),
  });

  // Borrar una sede deja sin sentido "Espacios de <esa sede>" si era la
  // seleccionada — se cierra el panel en vez de mostrar un panel de espacios
  // de una sede que ya no existe.
  const deleteVenue = async (id) => {
    if (await removeVenue(id) && selected?.id === id) setSelected(null);
  };

  const vCols = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 150 },
    { field: 'address', headerName: 'Direccion', flex: 1, minWidth: 150 },
    { field: 'isActive', headerName: 'Activa', width: 80, renderCell: ({ value }) => <Switch checked={value} disabled size="small" /> },
    { field: 'actions', headerName: 'Acciones', width: 110, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <RowActionsMenu
        primary={[
          { icon: 'eva:edit-fill', label: 'Editar', onClick: () => openEditVenue(row) },
          { icon: 'eva:trash-2-outline', label: 'Eliminar', color: 'error.main', onClick: () => deleteVenue(row.id) },
        ]}
        actions={[
          { icon: 'eva:grid-outline', label: 'Espacios', color: selected?.id === row.id ? 'primary.main' : 'text.secondary', onClick: () => setSelected(row) },
        ]}
      />
    )},
  ];

  const sCols = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 150 },
    { field: 'isActive', headerName: 'Activo', width: 80, renderCell: ({ value }) => <Switch checked={value} disabled size="small" /> },
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openEditSpace(row)} onDelete={() => removeSpace(row.id)} />
    )},
  ];

  return (
    <Box>
      <PageHeader title="Sedes y espacios" actionLabel="Nueva sede" onAction={openCreateVenue} />
      <Box sx={{ display: 'flex', gap: 3, flexDirection: { xs: 'column', md: 'row' } }}>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h6" fontWeight={600} sx={{ mb: 1 }}>Sedes</Typography>
          <DataGrid rows={venues || []} columns={vCols} loading={lVenues} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id}
            onRowClick={(p) => setSelected(p.row)} sx={{ cursor: 'pointer' }} />
        </Box>
        <Box sx={{ flex: 1 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
            <Typography variant="h6" fontWeight={600}>Espacios{selected ? ` de ${selected.name}` : ''}</Typography>
            {selected && <button onClick={openCreateSpace}>+ Nuevo espacio</button>}
          </Box>
          {sErr && <Alert severity="error" sx={{ mb: 1 }}>{sErr}</Alert>}
          {selected ? (
            <DataGrid rows={spaces || []} columns={sCols} loading={lSpaces} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />
          ) : (
            <Typography color="text.secondary" sx={{ py: 3 }}>Selecciona una sede.</Typography>
          )}
        </Box>
      </Box>

      <CrudDialog open={vOpen} editId={vEditId} entityName="Sede" onClose={closeVenue} onSave={saveVenue}>
        <TextField label="Nombre" value={vForm.name} onChange={(e) => setVForm({ ...vForm, name: e.target.value })} fullWidth required />
        <TextField label="Direccion" value={vForm.address} onChange={(e) => setVForm({ ...vForm, address: e.target.value })} fullWidth />
        <FormControlLabel control={<Switch checked={vForm.isActive} onChange={(e) => setVForm({ ...vForm, isActive: e.target.checked })} />} label="Activa" />
      </CrudDialog>

      <CrudDialog open={sOpen} editId={sEditId} entityName="Espacio" error={sErr} onClose={closeSpace} onSave={saveSpace}>
        <TextField label="Nombre" value={sForm.name} onChange={(e) => setSForm({ ...sForm, name: e.target.value })} fullWidth required helperText="Ej: Cancha 1, Pista A" />
        <FormControlLabel control={<Switch checked={sForm.isActive} onChange={(e) => setSForm({ ...sForm, isActive: e.target.checked })} />} label="Activo" />
      </CrudDialog>
    </Box>
  );
}
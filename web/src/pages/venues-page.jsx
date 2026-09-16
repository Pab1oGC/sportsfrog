import { useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Alert from '@mui/material/Alert';
import TextField from '@mui/material/TextField';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useCrudDialog } from 'src/hooks/use-crud';
import { apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { EstadoChip } from 'src/components/estado-chip';
import { useConfirm } from 'src/components/confirm-dialog';
import { LocationPicker } from 'src/components/location-picker';

const emptyVenueForm = () => ({ name: '', address: '', mapsUrl: '', isActive: true });
const emptySpaceForm = () => ({ name: '', isActive: true });

export default function VenuesPage() {
  const confirm = useConfirm();

  // Venues
  const {
    rows: venues, isLoading: lVenues, mutate: mutateVenues, open: vOpen, editId: vEditId, form: vForm, setForm: setVForm,
    openCreate: openCreateVenue, openEdit: openEditVenue, close: closeVenue, save: saveVenue, remove: removeVenue,
  } = useCrudDialog({
    resourceUrl: endpoints.venues,
    emptyForm: emptyVenueForm,
    entityName: 'sede',
    entityGender: 'f',
    buildUrl: (base, id) => endpoints.venue(id),
    mapToForm: (row) => ({ name: row.name, address: row.address || '', mapsUrl: row.mapsUrl || '', isActive: row.isActive !== false }),
  });

  // Spaces (depend on selected venue)
  const [selected, setSelected] = useState(null);
  const {
    rows: spaces, isLoading: lSpaces, mutate: mutateSpaces, open: sOpen, editId: sEditId, form: sForm, setForm: setSForm, error: sErr,
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

  // Activar es inofensivo y reversible con el mismo click, pero desactivar
  // saca la sede (y todos sus espacios con ella, ver ManageSpaces.IsAvailable
  // del lado del servidor) de la lista para partidos nuevos — vale la pena
  // avisar antes, no solo cambiar el estado en silencio. Lo que ya está
  // programado no se toca.
  const toggleVenueActive = async (row, event) => {
    event.stopPropagation();
    if (row.isActive) {
      const ok = await confirm(
        `Desactivar "${row.name}"? Deja de ofrecerse para partidos nuevos, junto con todos sus espacios. Los partidos que ya la tienen asignada no se ven afectados.`,
        { confirmLabel: 'Desactivar', danger: true },
      );
      if (!ok) return;
    }
    try {
      await apiPut(endpoints.venue(row.id), { name: row.name, address: row.address || null, mapsUrl: row.mapsUrl || null, isActive: !row.isActive });
      mutateVenues();
    } catch (err) { toast.error(err.message); }
  };

  const toggleSpaceActive = async (row) => {
    if (row.isActive) {
      const ok = await confirm(
        `Desactivar "${row.name}"? Deja de ofrecerse para partidos nuevos. Los partidos que ya lo tienen asignado no se ven afectados.`,
        { confirmLabel: 'Desactivar', danger: true },
      );
      if (!ok) return;
    }
    try {
      await apiPut(endpoints.space(row.id), { name: row.name, isActive: !row.isActive });
      mutateSpaces();
    } catch (err) { toast.error(err.message); }
  };

  const vCols = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 150 },
    { field: 'address', headerName: 'Direccion', flex: 1, minWidth: 150, renderCell: ({ value, row }) => (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, minWidth: 0 }}>
        <Box component="span" sx={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{value || '—'}</Box>
        {row.mapsUrl && (
          <Tooltip title="Ver en Google Maps">
            <IconButton size="small" component="a" href={row.mapsUrl} target="_blank" rel="noopener noreferrer" onClick={(e) => e.stopPropagation()}>
              <Iconify icon="mdi:map-marker" width={16} sx={{ color: 'primary.main' }} />
            </IconButton>
          </Tooltip>
        )}
      </Box>
    )},
    { field: 'isActive', headerName: 'Estado', width: 120, sortable: false, renderCell: ({ value, row }) => (
      <EstadoChip activo={value} femenino onClick={(e) => toggleVenueActive(row, e)} />
    )},
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
    { field: 'isActive', headerName: 'Estado', width: 120, sortable: false, renderCell: ({ value, row }) => (
      <EstadoChip activo={value} onClick={() => toggleSpaceActive(row)} />
    )},
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openEditSpace(row)} onDelete={() => removeSpace(row.id)} />
    )},
  ];

  return (
    <Box>
      <PageHeader title="Sedes y espacios" actionLabel="Nueva sede" onAction={openCreateVenue} />
      <Box sx={{ display: 'flex', gap: 3, flexDirection: { xs: 'column', md: 'row' } }}>
        <Paper variant="outlined" sx={{ flex: 1, p: 2 }}>
          <Typography variant="h6" fontWeight={600} sx={{ mb: 1.5 }}>Sedes</Typography>
          <DataGrid rows={venues || []} columns={vCols} loading={lVenues} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id}
            onRowClick={(p) => setSelected(p.row)} sx={{ cursor: 'pointer' }} />
        </Paper>
        <Paper variant="outlined" sx={{ flex: 1, p: 2 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1.5 }}>
            <Typography variant="h6" fontWeight={600}>Espacios{selected ? ` de ${selected.name}` : ''}</Typography>
            {selected && (
              <Button variant="contained" size="small" startIcon={<Iconify icon="eva:plus-fill" width={16} />} onClick={openCreateSpace}>
                Nuevo espacio
              </Button>
            )}
          </Box>
          {sErr && <Alert severity="error" sx={{ mb: 1 }}>{sErr}</Alert>}
          {selected ? (
            <DataGrid rows={spaces || []} columns={sCols} loading={lSpaces} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />
          ) : (
            <Typography color="text.secondary" sx={{ py: 3 }}>Selecciona una sede para ver y administrar sus espacios.</Typography>
          )}
        </Paper>
      </Box>

      <CrudDialog open={vOpen} editId={vEditId} entityName="Sede" entityGender="f" onClose={closeVenue} onSave={saveVenue} maxWidth="sm">
        <TextField label="Nombre" value={vForm.name} onChange={(e) => setVForm({ ...vForm, name: e.target.value })} fullWidth required />
        <TextField label="Direccion" value={vForm.address} onChange={(e) => setVForm({ ...vForm, address: e.target.value })} fullWidth />
        <LocationPicker value={vForm.mapsUrl} onChange={(mapsUrl) => setVForm({ ...vForm, mapsUrl })} />
      </CrudDialog>

      <CrudDialog open={sOpen} editId={sEditId} entityName="Espacio" error={sErr} onClose={closeSpace} onSave={saveSpace}>
        <TextField label="Nombre" value={sForm.name} onChange={(e) => setSForm({ ...sForm, name: e.target.value })} fullWidth required helperText="Ej: Cancha 1, Pista A" />
      </CrudDialog>
    </Box>
  );
}

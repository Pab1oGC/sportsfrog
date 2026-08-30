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
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { useConfirm } from 'src/components/confirm-dialog';
import { toast } from 'sonner';

export default function VenuesPage() {
  const confirm = useConfirm();
  // Venues
  const { data: venues, mutate: mVenues, isLoading: lVenues } = useApi(endpoints.venues);
  const [vOpen, setVOpen] = useState(false);
  const [vEdit, setVEdit] = useState(null);
  const [vForm, setVForm] = useState({ name: '', address: '', isActive: true });

  // Spaces (depend on selected venue)
  const [selected, setSelected] = useState(null);
  const { data: spaces, mutate: mSpaces, isLoading: lSpaces } = useApi(selected ? endpoints.venueSpaces(selected.id) : null);
  const [sOpen, setSOpen] = useState(false);
  const [sEdit, setSEdit] = useState(null);
  const [sForm, setSForm] = useState({ name: '', isActive: true });
  const [sErr, setSErr] = useState('');

  // Venue handlers
  const openVenue = (row) => {
    setVEdit(row || null);
    setVForm(row ? { name: row.name, address: row.address || '', isActive: row.isActive !== false } : { name: '', address: '', isActive: true });
    setVOpen(true);
  };

  const saveVenue = async () => {
    const url = vEdit ? endpoints.venue(vEdit.id) : endpoints.venues;
    // isActive es obligatorio para el backend y no tiene valor por defecto:
    // si no se manda, la sede queda inactiva en silencio y ninguno de sus
    // espacios aparece despues como disponible en ningun lado.
    vEdit ? await apiPut(url, vForm) : await apiPost(url, vForm);
    setVOpen(false); mVenues();
  };

  const deleteVenue = async (id) => {
    const ok = await confirm('Eliminar sede?', { confirmLabel: 'Eliminar', danger: true });
    if (!ok) return;
    await apiDelete(endpoints.venue(id));
    mVenues(); setSelected(null); toast.success('Sede eliminada.');
  };

  // Space handlers
  const openSpace = (row) => {
    setSEdit(row || null);
    setSForm(row ? { name: row.name, isActive: row.isActive } : { name: '', isActive: true });
    setSErr(''); setSOpen(true);
  };

  const saveSpace = async () => {
    if (!selected) return; setSErr('');
    try {
      const body = { name: sForm.name, isActive: sForm.isActive };
      sEdit ? await apiPut(endpoints.space(sEdit.id), body) : await apiPost(endpoints.venueSpaces(selected.id), body);
      setSOpen(false); mSpaces();
    } catch (err) { setSErr(err.message); }
  };

  const deleteSpace = async (id) => {
    const ok = await confirm('Eliminar espacio?', { confirmLabel: 'Eliminar', danger: true });
    if (!ok) return;
    await apiDelete(endpoints.space(id)); mSpaces(); toast.success('Espacio eliminado.');
  };

  const vCols = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 150 },
    { field: 'address', headerName: 'Direccion', flex: 1, minWidth: 150 },
    { field: 'isActive', headerName: 'Activa', width: 80, renderCell: ({ value }) => <Switch checked={value} disabled size="small" /> },
    { field: 'actions', headerName: '', width: 140, renderCell: ({ row }) => (
      <Box sx={{ display: 'flex' }}>
        <Tooltip title="Espacios"><IconButton size="small" onClick={() => setSelected(row)}>
          <Iconify icon="eva:grid-outline" width={18} sx={{ color: selected?.id === row.id ? 'primary.main' : 'text.secondary' }} />
        </IconButton></Tooltip>
        <Tooltip title="Editar"><IconButton size="small" onClick={() => openVenue(row)}>
          <Iconify icon="eva:edit-fill" width={18} />
        </IconButton></Tooltip>
        <Tooltip title="Eliminar"><IconButton size="small" onClick={() => deleteVenue(row.id)}>
          <Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} />
        </IconButton></Tooltip>
      </Box>
    )},
  ];

  const sCols = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 150 },
    { field: 'isActive', headerName: 'Activo', width: 80, renderCell: ({ value }) => <Switch checked={value} disabled size="small" /> },
    { field: 'actions', headerName: '', width: 100, renderCell: ({ row }) => (
      <Box sx={{ display: 'flex' }}>
        <Tooltip title="Editar"><IconButton size="small" onClick={() => openSpace(row)}>
          <Iconify icon="eva:edit-fill" width={18} />
        </IconButton></Tooltip>
        <Tooltip title="Eliminar"><IconButton size="small" onClick={() => deleteSpace(row.id)}>
          <Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} />
        </IconButton></Tooltip>
      </Box>
    )},
  ];

  return (
    <Box>
      <PageHeader title="Sedes y espacios" actionLabel="Nueva sede" onAction={() => openVenue(null)} />
      <Box sx={{ display: 'flex', gap: 3, flexDirection: { xs: 'column', md: 'row' } }}>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h6" fontWeight={600} sx={{ mb: 1 }}>Sedes</Typography>
          <DataGrid rows={venues || []} columns={vCols} loading={lVenues} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id}
            onRowClick={(p) => setSelected(p.row)} sx={{ cursor: 'pointer' }} />
        </Box>
        <Box sx={{ flex: 1 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
            <Typography variant="h6" fontWeight={600}>Espacios{selected ? ` de ${selected.name}` : ''}</Typography>
            {selected && <button onClick={() => openSpace(null)}>+ Nuevo espacio</button>}
          </Box>
          {sErr && <Alert severity="error" sx={{ mb: 1 }}>{sErr}</Alert>}
          {selected ? (
            <DataGrid rows={spaces || []} columns={sCols} loading={lSpaces} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />
          ) : (
            <Typography color="text.secondary" sx={{ py: 3 }}>Selecciona una sede.</Typography>
          )}
        </Box>
      </Box>

      <CrudDialog open={vOpen} editId={vEdit?.id} entityName="Sede" onClose={() => setVOpen(false)} onSave={saveVenue}>
        <TextField label="Nombre" value={vForm.name} onChange={(e) => setVForm({ ...vForm, name: e.target.value })} fullWidth required />
        <TextField label="Direccion" value={vForm.address} onChange={(e) => setVForm({ ...vForm, address: e.target.value })} fullWidth />
        <FormControlLabel control={<Switch checked={vForm.isActive} onChange={(e) => setVForm({ ...vForm, isActive: e.target.checked })} />} label="Activa" />
      </CrudDialog>

      <CrudDialog open={sOpen} editId={sEdit?.id} entityName="Espacio" error={sErr} onClose={() => setSOpen(false)} onSave={saveSpace}>
        <TextField label="Nombre" value={sForm.name} onChange={(e) => setSForm({ ...sForm, name: e.target.value })} fullWidth required helperText="Ej: Cancha 1, Pista A" />
        <FormControlLabel control={<Switch checked={sForm.isActive} onChange={(e) => setSForm({ ...sForm, isActive: e.target.checked })} />} label="Activo" />
      </CrudDialog>
    </Box>
  );
}
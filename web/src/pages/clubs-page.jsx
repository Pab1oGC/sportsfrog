import Avatar from '@mui/material/Avatar';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints } from 'src/lib/axios';
import { leerComoDataUrl } from 'src/lib/data-url';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';

// El logo viaja como data URL, igual que la foto de un deportista: el
// backend la normaliza y la guarda en el almacenamiento de objetos, no en la
// columna. Tres estados en el formulario, no dos:
//   - logoUrl: null      -> no se toca (por defecto, al editar)
//   - logoUrl: ''        -> quitar el logo actual
//   - logoUrl: 'data:..' -> reemplazarlo por este
// currentLogo es aparte y nunca se envia: es solo lo que ya hay, para la
// vista previa.
const emptyForm = () => ({ name: '', shortName: '', logoUrl: null, currentLogo: null, isActive: true });

export default function ClubsPage() {
  const { rows, isLoading, open, editId, form, setForm, error, saving, openCreate, openEdit, close, save, remove } = useCrudDialog({
    resourceUrl: endpoints.clubs,
    emptyForm,
    entityName: 'club',
    // isActive es obligatorio para el backend al editar y no tiene valor por
    // defecto: si no se manda, el club queda inactivo en silencio con
    // cualquier edicion, aunque nadie haya tocado el interruptor.
    mapToForm: (row) => ({ name: row.name || '', shortName: row.shortName || '', logoUrl: null, currentLogo: row.logoUrl || null, isActive: row.isActive !== false }),
    mapToSend: (f) => ({ name: f.name, shortName: f.shortName || null, logoUrl: f.logoUrl, isActive: f.isActive }),
  });

  const elegirLogo = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setForm({ ...form, logoUrl: await leerComoDataUrl(file) });
  };

  const quitarLogo = () => setForm({ ...form, logoUrl: '', currentLogo: null });

  const vistaPrevia = form.logoUrl || form.currentLogo;

  const columns = [
    { field: 'logoUrl', headerName: '', width: 60, sortable: false, renderCell: ({ value }) => (
      <Avatar src={value || undefined} variant="rounded" sx={{ width: 32, height: 32, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
        {!value && <Iconify icon="mdi:office-building-outline" width={18} sx={{ color: 'text.disabled' }} />}
      </Avatar>
    )},
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
      <DataGrid rows={rows} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} rowHeight={52} />
      <CrudDialog open={open} editId={editId} entityName="Club" error={error} saving={saving} onClose={close} onSave={save}>
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth />
        <TextField label="Abreviatura" value={form.shortName} onChange={(e) => setForm({ ...form, shortName: e.target.value })} fullWidth />

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          {/* object-fit: contain, no el "cover" que trae Avatar por defecto —
              un escudo que no es cuadrado se recortaba en vez de verse
              entero. */}
          <Avatar src={vistaPrevia || undefined} variant="rounded" sx={{ width: 56, height: 56, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
            {!vistaPrevia && <Iconify icon="mdi:office-building-outline" width={28} sx={{ color: 'text.disabled' }} />}
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
          PNG, JPEG o WebP. Opcional — un club sin logo se muestra con un icono generico.
        </Typography>
        <FormControlLabel control={<Switch checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />} label="Activo" />
      </CrudDialog>
    </Box>
  );
}

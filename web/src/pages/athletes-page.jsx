import Box from '@mui/material/Box';
import { useState } from 'react';
import TextField from '@mui/material/TextField';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { endpoints, default as axios } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { useConfirm } from 'src/components/confirm-dialog';
import { toast } from 'sonner';

// Cuantos años cumplidos tiene hoy, para que a simple vista se note quien
// necesita datos de apoderado sin tener que hacer la cuenta a mano.
function edad(birthDate) {
  if (!birthDate) return null;
  const nacimiento = new Date(birthDate);
  const hoy = new Date();
  let años = hoy.getFullYear() - nacimiento.getFullYear();
  const aunNoCumple = hoy.getMonth() < nacimiento.getMonth()
    || (hoy.getMonth() === nacimiento.getMonth() && hoy.getDate() < nacimiento.getDate());
  if (aunNoCumple) años -= 1;
  return años;
}

export default function AthletesPage() {
  const confirm = useConfirm();
  const { data, mutate, isLoading } = useApi(endpoints.athletes);

  const [open, setOpen] = useState(false);
  const [editId, setEditId] = useState(null);
  const [form, setForm] = useState({ firstName: '', lastName: '', documentId: '', birthDate: '', gender: '', guardianName: '', guardianPhone: '', isActive: true });
  const [error, setError] = useState('');

  const [photoOpen, setPhotoOpen] = useState(false);
  const [photoFile, setPhotoFile] = useState(null);
  const [photoLoading, setPhotoLoading] = useState(false);
  const [photoResult, setPhotoResult] = useState(null);

  const openDialog = (row) => {
    setEditId(row?.id || null);
    setForm(row
      ? { firstName: row.firstName, lastName: row.lastName, documentId: row.documentId, birthDate: row.birthDate || '', gender: row.gender || '', guardianName: row.guardianName || '', guardianPhone: row.guardianPhone || '', isActive: row.isActive !== false }
      : { firstName: '', lastName: '', documentId: '', birthDate: '', gender: '', guardianName: '', guardianPhone: '', isActive: true });
    setError(''); setOpen(true);
  };

  const save = async () => {
    setError('');
    try {
      const url = editId ? endpoints.athlete(editId) : endpoints.athletes;
      // Los opcionales viajan en null, no en '': el backend acepta "sin
      // definir" pero no una cadena vacia, que para el validador es un
      // valor invalido en vez de una ausencia. isActive tambien viaja
      // siempre (viene de form): al editar es obligatorio para el backend y
      // sin valor por defecto — si no se manda, el deportista queda inactivo
      // en silencio con cualquier edicion, y un deportista inactivo no se
      // puede inscribir en ningun equipo.
      const body = {
        ...form,
        gender: form.gender || null,
        guardianName: form.guardianName || null,
        guardianPhone: form.guardianPhone || null,
      };
      editId ? await apiPut(url, body) : await apiPost(url, body);
      setOpen(false); mutate(); toast.success('Deportista guardado.');
    } catch (err) { setError(err.message); }
  };

  const remove = async (id) => {
    const ok = await confirm('Eliminar deportista?', { confirmLabel: 'Eliminar', danger: true });
    if (!ok) return;
    await apiDelete(endpoints.athlete(id)); mutate(); toast.success('Deportista eliminado.');
  };

  const uploadPhotos = async () => {
    if (!photoFile) return;
    setPhotoLoading(true); setPhotoResult(null);
    try {
      const fd = new FormData(); fd.append('file', photoFile);
      const token = sessionStorage.getItem('jwt_access_token');
      const res = await axios.post(endpoints.athletePhotos, fd, { headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${token}` } });
      setPhotoResult(res.data);
    } catch (err) { setPhotoResult({ error: err.message }); }
    finally { setPhotoLoading(false); }
  };

  const columns = [
    { field: 'firstName', headerName: 'Nombres', flex: 1, minWidth: 150 },
    { field: 'lastName', headerName: 'Apellidos', flex: 1, minWidth: 150 },
    { field: 'documentId', headerName: 'Documento', width: 140 },
    { field: 'birthDate', headerName: 'Fecha nac.', width: 120 },
    { field: 'edad', headerName: 'Edad', width: 70, valueGetter: (_, row) => edad(row.birthDate), renderCell: ({ value }) => (
      value != null ? <span style={{ color: value < 18 ? 'var(--mui-palette-warning-main, #b26a00)' : undefined, fontWeight: value < 18 ? 700 : 400 }}>{value}</span> : '--'
    ) },
    { field: 'gender', headerName: 'Genero', width: 80 },
    { field: 'guardianName', headerName: 'Apoderado', width: 150, renderCell: ({ value }) => value || '--' },
    { field: 'isActive', headerName: 'Activo', width: 80, renderCell: ({ value }) => <Switch checked={value} disabled size="small" /> },
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openDialog(row)} onDelete={() => remove(row.id)} />
    )},
  ];

  return (
    <div>
      <PageHeader title="Deportistas" actionLabel="Nuevo" onAction={() => openDialog(null)}>
        <Button variant="outlined" startIcon={<Iconify icon="eva:image-outline" />} onClick={() => { setPhotoFile(null); setPhotoResult(null); setPhotoOpen(true); }}>Importar fotos</Button>
      </PageHeader>
      <DataGrid rows={data || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick />

      <CrudDialog open={open} editId={editId} entityName="Deportista" error={error} onClose={() => setOpen(false)} onSave={save}>
        <TextField label="Nombres" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} fullWidth />
        <TextField label="Apellidos" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} fullWidth />
        <TextField label="Documento" value={form.documentId} onChange={(e) => setForm({ ...form, documentId: e.target.value })} fullWidth />
        <TextField label="Fecha nacimiento" type="date" value={form.birthDate} onChange={(e) => setForm({ ...form, birthDate: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
        <TextField label="Genero" value={form.gender} onChange={(e) => setForm({ ...form, gender: e.target.value })} fullWidth />
        {edad(form.birthDate) != null && edad(form.birthDate) < 18 && (
          <Alert severity="warning">
            Es menor de edad ({edad(form.birthDate)} años): conviene completar los datos del apoderado.
          </Alert>
        )}
        <TextField label="Nombre del apoderado" value={form.guardianName} onChange={(e) => setForm({ ...form, guardianName: e.target.value })} fullWidth helperText="Padre, madre o tutor. Solo se usa dentro de la organizacion, nunca se publica." />
        <TextField label="Telefono del apoderado" value={form.guardianPhone} onChange={(e) => setForm({ ...form, guardianPhone: e.target.value })} fullWidth />
        <FormControlLabel control={<Switch checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />} label="Activo" />
      </CrudDialog>

      {photoOpen && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', zIndex: 1300, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 16, boxSizing: 'border-box' }} onClick={() => setPhotoOpen(false)}>
          <Box sx={{ bgcolor: 'background.paper', borderRadius: 2, p: 3, width: { xs: '100%', sm: 400 }, maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
            <h3 style={{ margin: '0 0 12px' }}>Importar fotos (ZIP)</h3>
            <Alert severity="info" sx={{ mb: 2 }}>Cada imagen debe llamarse igual al documento del deportista.</Alert>
            <Button variant="outlined" component="label" startIcon={<Iconify icon="eva:upload-outline" />}>
              Seleccionar ZIP
              <input type="file" accept=".zip" hidden onChange={(e) => setPhotoFile(e.target.files[0])} />
            </Button>
            {photoFile && <p style={{ marginTop: 8 }}>{photoFile.name}</p>}
            {photoResult && !photoResult.error && <Alert severity="success" sx={{ mt: 2 }}>Lote enviado. Estado: {photoResult.status}.</Alert>}
            {photoResult?.error && <Alert severity="error" sx={{ mt: 2 }}>{photoResult.error}</Alert>}
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
              <button onClick={() => setPhotoOpen(false)}>Cerrar</button>
              <button onClick={uploadPhotos} disabled={!photoFile || photoLoading}>{photoLoading ? 'Subiendo...' : 'Subir'}</button>
            </div>
          </Box>
        </div>
      )}
    </div>
  );
}
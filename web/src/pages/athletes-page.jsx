import { useState } from 'react';
import TextField from '@mui/material/TextField';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { endpoints, default as axios } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { toast } from 'sonner';

export default function AthletesPage() {
  const { data, mutate, isLoading } = useApi(endpoints.athletes);

  const [open, setOpen] = useState(false);
  const [editId, setEditId] = useState(null);
  const [form, setForm] = useState({ firstName: '', lastName: '', documentId: '', birthDate: '', gender: '' });
  const [error, setError] = useState('');

  const [photoOpen, setPhotoOpen] = useState(false);
  const [photoFile, setPhotoFile] = useState(null);
  const [photoLoading, setPhotoLoading] = useState(false);
  const [photoResult, setPhotoResult] = useState(null);

  const openDialog = (row) => {
    setEditId(row?.id || null);
    setForm(row ? { firstName: row.firstName, lastName: row.lastName, documentId: row.documentId, birthDate: row.birthDate || '', gender: row.gender || '' }
      : { firstName: '', lastName: '', documentId: '', birthDate: '', gender: '' });
    setError(''); setOpen(true);
  };

  const save = async () => {
    setError('');
    try {
      const url = editId ? endpoints.athlete(editId) : endpoints.athletes;
      editId ? await apiPut(url, form) : await apiPost(url, form);
      setOpen(false); mutate(); toast.success('Deportista guardado.');
    } catch (err) { setError(err.message); }
  };

  const remove = async (id) => {
    if (!confirm('Eliminar deportista?')) return;
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
    { field: 'gender', headerName: 'Genero', width: 80 },
    { field: 'actions', headerName: '', width: 100, renderCell: ({ row }) => (
      <div style={{ display: 'flex', gap: 4 }}>
        <Iconify icon="eva:edit-fill" sx={{ cursor: 'pointer', color: 'text.secondary' }} onClick={() => openDialog(row)} />
        <Iconify icon="eva:trash-2-outline" sx={{ cursor: 'pointer', color: 'error.main' }} onClick={() => remove(row.id)} />
      </div>
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
      </CrudDialog>

      {photoOpen && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', zIndex: 1300, display: 'flex', alignItems: 'center', justifyContent: 'center' }} onClick={() => setPhotoOpen(false)}>
          <div style={{ background: 'white', borderRadius: 8, padding: 24, minWidth: 400 }} onClick={(e) => e.stopPropagation()}>
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
          </div>
        </div>
      )}
    </div>
  );
}
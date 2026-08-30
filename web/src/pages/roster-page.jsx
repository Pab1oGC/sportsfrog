import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Alert from '@mui/material/Alert';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Stepper from '@mui/material/Stepper';
import Step from '@mui/material/Step';
import StepLabel from '@mui/material/StepLabel';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints, default as axios } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { SelectionCompetition, SelectionCategory, SelectionTeam, SelectionClub } from 'src/components/selectors';
import { useConfirm } from 'src/components/confirm-dialog';
import { toast } from 'sonner';

const PREVIEW_COLS = [
  { field: 'number', headerName: '#', width: 50 },
  { field: 'document', headerName: 'Documento', width: 120 },
  { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 150 },
  { field: 'outcome', headerName: 'Accion', width: 120, renderCell: ({ value }) => {
    const colors = { register: 'success', create_and_register: 'info', already_registered: 'default', rejected: 'error' };
    const labels = { register: 'Registrar', create_and_register: 'Crear+Registrar', already_registered: 'Ya registrado', rejected: 'Rechazado' };
    return <Chip label={labels[value] || value} color={colors[value] || 'default'} size="small" />;
  }},
  { field: 'problems', headerName: 'Problemas', flex: 1, renderCell: ({ value }) => (value || []).join('; ') || '--' },
];

export default function RosterPage() {
  const confirm = useConfirm();
  const cascade = useCascade();
  const { data: roster, mutate, isLoading } = useApi(cascade.teamId ? endpoints.roster(cascade.teamId) : null);
  const { data: athletes } = useApi(endpoints.athletes);

  const [open, setOpen] = useState(false);
  const [editEntry, setEditEntry] = useState(null);
  const [form, setForm] = useState({ athleteId: '', jerseyNumber: '', position: '' });
  const [error, setError] = useState('');

  const [impOpen, setImpOpen] = useState(false);
  const [impStep, setImpStep] = useState(0);
  const [impFile, setImpFile] = useState(null);
  const [impResult, setImpResult] = useState(null);
  const [impLoading, setImpLoading] = useState(false);

  const active = (roster || []).filter((e) => !e.withdrawnAt);

  const openRegister = () => { setEditEntry(null); setForm({ athleteId: '', jerseyNumber: '', position: '' }); setError(''); setOpen(true); };
  const openEdit = (entry) => { setEditEntry(entry); setForm({ athleteId: entry.athleteId, jerseyNumber: entry.jerseyNumber || '', position: entry.position || '' }); setError(''); setOpen(true); };

  const save = async () => {
    if (!cascade.teamId) return; setError('');
    try {
      const body = { jerseyNumber: form.jerseyNumber ? Number(form.jerseyNumber) : null, position: form.position || null };
      editEntry ? await apiPut(endpoints.rosterEntry(editEntry.id), body) : await apiPost(endpoints.roster(cascade.teamId), { ...body, athleteId: form.athleteId });
      setOpen(false); mutate();
    } catch (err) { setError(err.message); }
  };

  const withdraw = async (entry, w) => {
    try { await apiPut(endpoints.rosterWithdrawal(entry.id), { withdrawn: w }); mutate(); }
    catch (err) { toast.error(err.message); }
  };

  const strike = async (entry) => {
    const ok = await confirm('Anular registro?', { confirmLabel: 'Anular', danger: true });
    if (!ok) return;
    try { await apiDelete(endpoints.rosterEntry(entry.id)); mutate(); } catch (err) { toast.error(err.message); }
  };

  const downloadTemplate = async () => {
    if (!cascade.teamId) return;
    try {
      const res = await axios.get(endpoints.rosterTemplate(cascade.teamId), { responseType: 'blob' });
      downloadBlob(res, 'planilla-nomina.xlsx');
    } catch (err) { toast.error(err.message); }
  };

  const previewImport = async () => {
    if (!impFile || !cascade.teamId) return;
    setImpLoading(true);
    try {
      const fd = new FormData(); fd.append('file', impFile);
      const token = sessionStorage.getItem('jwt_access_token');
      const res = await axios.post(endpoints.rosterImportPreview(cascade.teamId), fd, { headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${token}` } });
      setImpResult(res.data); setImpStep(1);
    } catch (err) { toast.error(err.message); } finally { setImpLoading(false); }
  };

  const applyImport = async () => {
    if (!impFile || !cascade.teamId) return;
    setImpLoading(true);
    try {
      const fd = new FormData(); fd.append('file', impFile);
      const token = sessionStorage.getItem('jwt_access_token');
      const res = await axios.post(endpoints.rosterImportApply(cascade.teamId), fd, { headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${token}` } });
      setImpResult(res.data); setImpStep(2); mutate();
    } catch (err) { toast.error(err.message); } finally { setImpLoading(false); }
  };

  const columns = [
    { field: 'jerseyNumber', headerName: '#', width: 60 },
    { field: 'lastName', headerName: 'Apellido', flex: 1, minWidth: 120 },
    { field: 'firstName', headerName: 'Nombre', flex: 1, minWidth: 120 },
    { field: 'documentId', headerName: 'Documento', width: 120 },
    { field: 'position', headerName: 'Posicion', width: 100, renderCell: ({ value }) => value || '--' },
    { field: 'withdrawnAt', headerName: 'Estado', width: 110, renderCell: ({ value }) => value ? <Chip label="Retirado" color="warning" size="small" /> : <Chip label="Activo" color="success" size="small" /> },
    { field: 'actions', headerName: '', width: 160, renderCell: ({ row: entry }) => {
      const w = !!entry.withdrawnAt;
      return (
        <Box sx={{ display: 'flex' }}>
          <Tooltip title="Editar"><IconButton size="small" onClick={() => openEdit(entry)}><Iconify icon="eva:edit-fill" width={18} /></IconButton></Tooltip>
          <Tooltip title={w ? 'Reintegrar' : 'Retirar'}><IconButton size="small" onClick={() => withdraw(entry, !w)}><Iconify icon={w ? 'eva:undo-fill' : 'eva:person-done-outline'} width={18} sx={{ color: w ? 'info.main' : 'warning.main' }} /></IconButton></Tooltip>
          {!w && <Tooltip title="Anular"><IconButton size="small" onClick={() => strike(entry)}><Iconify icon="eva:close-circle-outline" width={18} sx={{ color: 'error.main' }} /></IconButton></Tooltip>}
        </Box>
      );
    }},
  ];

  return (
    <Box>
      <PageHeader title="Nomina de jugadores">
        <Button variant="outlined" startIcon={<Iconify icon="eva:download-outline" />} onClick={downloadTemplate} disabled={!cascade.teamId}>Plantilla</Button>
        <Button variant="outlined" startIcon={<Iconify icon="eva:upload-outline" />} onClick={() => { setImpFile(null); setImpResult(null); setImpStep(0); setImpOpen(true); }} disabled={!cascade.teamId}>Importar Excel</Button>
        <Button variant="contained" startIcon={<Iconify icon="eva:plus-fill" />} onClick={openRegister} disabled={!cascade.teamId}>Registrar</Button>
      </PageHeader>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={cascade.compId} onChange={(e) => cascade.setCompId(e.target.value)} required /></Box>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCategory competitionId={cascade.compId} value={cascade.catId} onChange={(e) => cascade.setCatId(e.target.value)} required /></Box>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionTeam categoryId={cascade.catId} value={cascade.teamId} onChange={(e) => cascade.setTeamId(e.target.value)} required /></Box>
      </Box>
      {cascade.teamId && <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{active.length} activo(s) / {(roster || []).length} total</Typography>}
      <DataGrid rows={roster || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />

      <CrudDialog open={open} editId={editEntry?.id} entityName="Jugador" error={error} onClose={() => setOpen(false)} onSave={save}>
        {!editEntry && (
          <TextField select label="Deportista" value={form.athleteId} onChange={(e) => setForm({ ...form, athleteId: e.target.value })} fullWidth required>
            <MenuItem value="">Seleccionar</MenuItem>
            {(athletes || []).map((a) => <MenuItem key={a.id} value={a.id}>{a.lastName}, {a.firstName}</MenuItem>)}
          </TextField>
        )}
        <TextField label="Numero de camiseta" type="number" value={form.jerseyNumber} onChange={(e) => setForm({ ...form, jerseyNumber: e.target.value })} fullWidth />
        <TextField label="Posicion" value={form.position} onChange={(e) => setForm({ ...form, position: e.target.value })} fullWidth />
      </CrudDialog>

      <ExcelImportDialog open={impOpen} onClose={() => setImpOpen(false)} step={impStep} file={impFile} result={impResult} loading={impLoading}
        onFileChange={(f) => setImpFile(f)} onPreview={previewImport} onApply={applyImport} />
    </Box>
  );
}

function ExcelImportDialog({ open, onClose, step, file, result, loading, onFileChange, onPreview, onApply }) {
  return (
    <Box component="div">
      {open && (
        <Box component="div" sx={{ position: 'fixed', inset: 0, bgcolor: 'rgba(0,0,0,0.5)', zIndex: 1300, display: 'flex', alignItems: 'center', justifyContent: 'center', p: 2 }} onClick={onClose}>
          <Box component="div" sx={{ bgcolor: 'background.paper', borderRadius: 2, p: 3, width: { xs: '100%', sm: 500 }, maxWidth: 900, maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
            <Typography variant="h6" fontWeight={700} sx={{ mb: 2 }}>Importar nomina desde Excel</Typography>
            <Stepper activeStep={step} sx={{ mb: 3 }}>
              <Step><StepLabel>Subir archivo</StepLabel></Step>
              <Step><StepLabel>Vista previa</StepLabel></Step>
              <Step><StepLabel>Aplicar</StepLabel></Step>
            </Stepper>
            {step === 0 && (
              <Box>
                <Alert severity="info" sx={{ mb: 2 }}>Descarga la plantilla, completala, y sube el archivo.</Alert>
                <Button variant="outlined" component="label" startIcon={<Iconify icon="eva:upload-outline" />}>
                  Seleccionar archivo
                  <input type="file" accept=".xlsx,.xls" hidden onChange={(e) => onFileChange(e.target.files[0])} />
                </Button>
                {file && <Typography variant="body2" sx={{ mt: 1 }}>Archivo: {file.name}</Typography>}
              </Box>
            )}
            {step === 1 && result && (
              <DataGrid rows={(result.rows || []).map((r, i) => ({ ...r, id: i }))} columns={PREVIEW_COLS} autoHeight hideFooter disableRowSelectionOnClick sx={{ mb: 2 }} />
            )}
            {step === 2 && result && (
              <Alert severity="success">Registrados: {result.registered}, Creados: {result.created}, Ya existentes: {result.alreadyRegistered}, Rechazados: {result.rejected}</Alert>
            )}
            <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, mt: 2 }}>
              <Button onClick={onClose}>Cerrar</Button>
              {step === 0 && <Button variant="contained" onClick={onPreview} disabled={!file || loading}>Previsualizar</Button>}
              {step === 1 && <Button variant="contained" color="success" onClick={onApply} disabled={loading}>Aplicar</Button>}
            </Box>
          </Box>
        </Box>
      )}
    </Box>
  );
}

function downloadBlob(res, fallback) {
  const url = window.URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = url;
  const disp = res.headers?.['content-disposition'] || '';
  const match = /filename\*=UTF-8''([^;]+)/i.exec(disp) || /filename="?([^";]+)"?/i.exec(disp);
  a.download = match ? decodeURIComponent(match[1] || match[0]) : fallback;
  document.body.appendChild(a); a.click(); document.body.removeChild(a);
  setTimeout(() => window.URL.revokeObjectURL(url), 1000);
}